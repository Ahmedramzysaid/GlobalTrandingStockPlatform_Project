package com.gstp.trading.model;

import jakarta.persistence.*;
import lombok.*;
import java.math.BigDecimal;
import java.time.Instant;
import java.util.UUID;

@Entity
@Table(name = "trades")
@Data @Builder @NoArgsConstructor @AllArgsConstructor
public class Trade {
    @Id
    @Column(name = "id")
    private UUID id;

    @Column(name = "symbol", nullable = false)
    private String symbol;

    @Column(name = "buy_order_id", nullable = false)
    private UUID buyOrderId;

    @Column(name = "sell_order_id", nullable = false)
    private UUID sellOrderId;

    @Column(name = "buyer_user_id", nullable = false)
    private UUID buyerUserId;

    @Column(name = "seller_user_id", nullable = false)
    private UUID sellerUserId;

    @Column(name = "price", nullable = false, precision = 18, scale = 8)
    private BigDecimal price;

    @Column(name = "quantity", nullable = false, precision = 18, scale = 8)
    private BigDecimal quantity;

    @Column(name = "commission", precision = 18, scale = 8)
    private BigDecimal commission;

    @Column(name = "executed_at")
    private Instant executedAt;

    @PrePersist
    public void prePersist() {
        if (id == null) id = UUID.randomUUID();
        if (executedAt == null) executedAt = Instant.now();
        if (commission == null) commission = BigDecimal.ZERO;
    }
}
