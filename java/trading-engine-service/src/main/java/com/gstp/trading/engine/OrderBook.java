package com.gstp.trading.engine;

import com.gstp.trading.model.Order;
import com.gstp.trading.model.Trade;
import lombok.Getter;
import java.math.BigDecimal;
import java.util.*;

/**
 * Order Book for a single symbol.
 * Contains buy-side bids and sell-side asks.
 */
@Getter
public class OrderBook {
    private final String symbol;
    private final PriceTimePriorityQueue bids;
    private final PriceTimePriorityQueue asks;

    public OrderBook(String symbol) {
        this.symbol = symbol;
        this.bids = new PriceTimePriorityQueue(Order.OrderSide.BUY);
        this.asks = new PriceTimePriorityQueue(Order.OrderSide.SELL);
    }

    public Map<String, Object> getSnapshot(int depth) {
        return Map.of(
                "symbol", symbol,
                "bids", bids.getTopLevels(depth),
                "asks", asks.getTopLevels(depth),
                "bidCount", bids.size(),
                "askCount", asks.size()
        );
    }
}
