package com.gstp.risk.model;

import jakarta.persistence.*;
import lombok.*;
import java.math.BigDecimal;
import java.time.Instant;
import java.util.UUID;

@Entity @Table(name = "position_limits") @Data @NoArgsConstructor @AllArgsConstructor @Builder
public class PositionLimit {
    @Id private UUID id;
    @Column(name = "user_id", nullable = false) private UUID userId;
    @Column(length = 10) private String symbol;
    @Column(name = "max_quantity", precision = 18, scale = 8) private BigDecimal maxQuantity;
    @Column(name = "max_value", precision = 18, scale = 2) private BigDecimal maxValue;
    @Column(name = "current_quantity", precision = 18, scale = 8) private BigDecimal currentQuantity = BigDecimal.ZERO;
    @Column(name = "current_value", precision = 18, scale = 2) private BigDecimal currentValue = BigDecimal.ZERO;
    @Column(name = "updated_at") private Instant updatedAt = Instant.now();
    @PrePersist public void pp() { if (id == null) id = UUID.randomUUID(); }
}
