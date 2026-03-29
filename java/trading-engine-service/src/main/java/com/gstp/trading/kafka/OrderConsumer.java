package com.gstp.trading.kafka;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.gstp.trading.engine.MatchingEngine;
import com.gstp.trading.model.Order;
import com.gstp.trading.model.Trade;
import com.gstp.trading.repository.TradeRepository;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.kafka.annotation.KafkaListener;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.stereotype.Component;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.List;
import java.util.UUID;

@Slf4j
@Component
@RequiredArgsConstructor
public class OrderConsumer {

    private final MatchingEngine matchingEngine;
    private final KafkaTemplate<String, String> kafkaTemplate;
    private final TradeRepository tradeRepository;
    private final ObjectMapper objectMapper = new ObjectMapper();

    @KafkaListener(topics = "orders.created", groupId = "trading-engine")
    public void handleOrderCreated(String message) {
        try {
            JsonNode root = objectMapper.readTree(message);
            JsonNode data = root.get("Data");

            Order order = Order.builder()
                    .id(UUID.fromString(data.get("OrderId").asText()))
                    .userId(UUID.fromString(data.get("UserId").asText()))
                    .symbol(data.get("Symbol").asText())
                    .side(Order.OrderSide.valueOf(data.get("Side").asText()))
                    .type(Order.OrderType.valueOf(data.get("Type").asText()))
                    .quantity(new BigDecimal(data.get("Quantity").asText()))
                    .remainingQuantity(new BigDecimal(data.get("Quantity").asText()))
                    .price(data.has("Price") && !data.get("Price").isNull()
                            ? new BigDecimal(data.get("Price").asText()) : BigDecimal.ZERO)
                    .timestamp(Instant.now())
                    .build();

            log.info("Received order: {} {} {} @ {}",
                    order.getSide(), order.getQuantity(), order.getSymbol(), order.getPrice());

            List<Trade> trades = matchingEngine.processOrder(order);

            // Persist and publish each trade
            for (Trade trade : trades) {
                tradeRepository.save(trade);

                String tradeEvent = objectMapper.writeValueAsString(new TradeEvent(
                        trade.getId(), trade.getBuyOrderId(), trade.getSellOrderId(),
                        trade.getSymbol(), trade.getPrice(), trade.getQuantity(),
                        trade.getBuyerUserId(), trade.getSellerUserId(), trade.getExecutedAt()
                ));
                kafkaTemplate.send("trades.executed", trade.getSymbol(), tradeEvent);
                log.info("Trade executed: {} {} @ {}", trade.getQuantity(), trade.getSymbol(), trade.getPrice());
            }

        } catch (Exception e) {
            log.error("Error processing order: {}", e.getMessage(), e);
        }
    }

    @KafkaListener(topics = "orders.cancelled", groupId = "trading-engine")
    public void handleOrderCancelled(String message) {
        try {
            JsonNode root = objectMapper.readTree(message);
            String orderId = root.get("OrderId").asText();
            String symbol = root.get("Symbol").asText();
            boolean removed = matchingEngine.cancelOrder(symbol, UUID.fromString(orderId));
            log.info("Order cancellation {}: {}", removed ? "successful" : "not found", orderId);
        } catch (Exception e) {
            log.error("Error cancelling order: {}", e.getMessage(), e);
        }
    }

    // Trade event DTO for Kafka
    public record TradeEvent(UUID TradeId, UUID BuyOrderId, UUID SellOrderId,
                              String Symbol, BigDecimal Price, BigDecimal Quantity,
                              UUID BuyerUserId, UUID SellerUserId, Instant Timestamp) {}
}
