package com.gstp.settlement.model;

import jakarta.persistence.*;
import lombok.*;
import java.math.BigDecimal;
import java.time.Instant;
import java.time.LocalDate;
import java.util.UUID;

@Entity @Table(name = "settlements") @Data @NoArgsConstructor @AllArgsConstructor @Builder
public class Settlement {
    @Id private UUID id;
    @Column(name = "trade_id", nullable = false) private UUID tradeId;
    @Column(name = "buyer_user_id", nullable = false) private UUID buyerUserId;
    @Column(name = "seller_user_id", nullable = false) private UUID sellerUserId;
    @Column(length = 10, nullable = false) private String symbol;
    @Column(precision = 18, scale = 8, nullable = false) private BigDecimal quantity;
    @Column(precision = 18, scale = 8, nullable = false) private BigDecimal price;
    @Column(name = "total_amount", precision = 18, scale = 2, nullable = false) private BigDecimal totalAmount;
    @Column(length = 20) private String status = "PENDING";
    @Column(name = "settlement_date", nullable = false) private LocalDate settlementDate;
    @Column(name = "created_at") private Instant createdAt = Instant.now();
    @Column(name = "settled_at") private Instant settledAt;
    @PrePersist public void pp() { if (id == null) id = UUID.randomUUID(); }
}
