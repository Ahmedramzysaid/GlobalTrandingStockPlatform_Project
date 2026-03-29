package com.gstp.trading.engine;

import com.gstp.trading.model.Order;
import com.gstp.trading.model.Trade;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.*;
import java.util.concurrent.ConcurrentHashMap;

/**
 * Core Matching Engine — Price-Time Priority (FIFO).
 * Industry-standard algorithm used by NASDAQ, NYSE, etc.
 * 
 * - Per-symbol locking for concurrency (not global lock).
 * - Market orders match immediately at best available price.
 * - Limit orders match if price crosses, remainder rests on book.
 */
@Slf4j
@Service
public class MatchingEngine {

    private final ConcurrentHashMap<String, OrderBook> orderBooks = new ConcurrentHashMap<>();

    public List<Trade> processOrder(Order incomingOrder) {
        OrderBook book = orderBooks.computeIfAbsent(
                incomingOrder.getSymbol(), OrderBook::new);

        List<Trade> trades;
        synchronized (book) {
            trades = match(incomingOrder, book);
        }

        log.info("Processed order {} for {} — {} trades executed",
                incomingOrder.getId(), incomingOrder.getSymbol(), trades.size());
        return trades;
    }

    private List<Trade> match(Order incoming, OrderBook book) {
        List<Trade> trades = new ArrayList<>();

        PriceTimePriorityQueue oppositeQueue = (incoming.getSide() == Order.OrderSide.BUY)
                ? book.getAsks()
                : book.getBids();

        BigDecimal remainingQty = incoming.getRemainingQuantity();

        while (remainingQty.compareTo(BigDecimal.ZERO) > 0) {
            Order bestOpposite = oppositeQueue.peekBest();
            if (bestOpposite == null) break;
            if (!canMatch(incoming, bestOpposite)) break;

            BigDecimal fillQty = remainingQty.min(bestOpposite.getRemainingQuantity());
            BigDecimal tradePrice = bestOpposite.getPrice(); // Price improvement for aggressor

            Trade trade = Trade.builder()
                    .id(UUID.randomUUID())
                    .symbol(incoming.getSymbol())
                    .price(tradePrice)
                    .quantity(fillQty)
                    .buyOrderId(incoming.getSide() == Order.OrderSide.BUY ? incoming.getId() : bestOpposite.getId())
                    .sellOrderId(incoming.getSide() == Order.OrderSide.SELL ? incoming.getId() : bestOpposite.getId())
                    .buyerUserId(incoming.getSide() == Order.OrderSide.BUY ? incoming.getUserId() : bestOpposite.getUserId())
                    .sellerUserId(incoming.getSide() == Order.OrderSide.SELL ? incoming.getUserId() : bestOpposite.getUserId())
                    .commission(fillQty.multiply(tradePrice).multiply(new BigDecimal("0.005"))) // 0.5% commission
                    .executedAt(Instant.now())
                    .build();

            trades.add(trade);
            remainingQty = remainingQty.subtract(fillQty);

            bestOpposite.fill(fillQty);
            if (bestOpposite.isFilled()) {
                oppositeQueue.pollBest();
            }
        }

        // If remaining quantity and it's a LIMIT order, rest on the book
        if (remainingQty.compareTo(BigDecimal.ZERO) > 0
                && incoming.getType() == Order.OrderType.LIMIT) {
            incoming.setRemainingQuantity(remainingQty);
            if (incoming.getSide() == Order.OrderSide.BUY) {
                book.getBids().addOrder(incoming);
            } else {
                book.getAsks().addOrder(incoming);
            }
            log.info("Order {} resting on book: {} remaining @ {}",
                    incoming.getId(), remainingQty, incoming.getPrice());
        }

        return trades;
    }

    private boolean canMatch(Order incoming, Order resting) {
        if (incoming.getType() == Order.OrderType.MARKET) return true;
        if (incoming.getSide() == Order.OrderSide.BUY) {
            return incoming.getPrice().compareTo(resting.getPrice()) >= 0;
        } else {
            return incoming.getPrice().compareTo(resting.getPrice()) <= 0;
        }
    }

    public boolean cancelOrder(String symbol, UUID orderId) {
        OrderBook book = orderBooks.get(symbol);
        if (book == null) return false;
        synchronized (book) {
            return book.getBids().removeOrder(orderId) || book.getAsks().removeOrder(orderId);
        }
    }

    public Map<String, Object> getOrderBookSnapshot(String symbol, int depth) {
        OrderBook book = orderBooks.get(symbol);
        if (book == null) return Map.of("symbol", symbol, "bids", List.of(), "asks", List.of());
        synchronized (book) {
            return book.getSnapshot(depth);
        }
    }
}
