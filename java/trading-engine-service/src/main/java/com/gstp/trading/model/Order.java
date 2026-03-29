package com.gstp.trading.model;

import lombok.*;
import java.math.BigDecimal;
import java.time.Instant;
import java.util.UUID;

@Data @Builder @NoArgsConstructor @AllArgsConstructor
public class Order {
    private UUID id;
    private UUID userId;
    private String symbol;
    private OrderSide side;
    private OrderType type;
    private BigDecimal quantity;
    private BigDecimal remainingQuantity;
    private BigDecimal price;
    private Instant timestamp;

    public boolean isFilled() {
        return remainingQuantity.compareTo(BigDecimal.ZERO) <= 0;
    }

    public void fill(BigDecimal fillQty) {
        this.remainingQuantity = this.remainingQuantity.subtract(fillQty);
    }

    public enum OrderSide { BUY, SELL }
    public enum OrderType { MARKET, LIMIT, STOP_LOSS, STOP_LIMIT }
}
