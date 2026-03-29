package com.gstp.portfolio.model;

import jakarta.persistence.*;
import lombok.*;
import java.math.BigDecimal;
import java.time.Instant;
import java.util.UUID;

@Entity @Table(name = "holdings", uniqueConstraints = @UniqueConstraint(columnNames = {"portfolio_id", "symbol"}))
@Data @NoArgsConstructor @AllArgsConstructor @Builder
public class Holding {
    @Id private UUID id;
    @ManyToOne @JoinColumn(name = "portfolio_id", nullable = false) private Portfolio portfolio;
    @Column(nullable = false, length = 10) private String symbol;
    @Column(precision = 18, scale = 8) private BigDecimal quantity = BigDecimal.ZERO;
    @Column(name = "avg_cost", precision = 18, scale = 8) private BigDecimal avgCost = BigDecimal.ZERO;
    @Column(name = "current_price", precision = 18, scale = 8) private BigDecimal currentPrice;
    @Column(name = "unrealized_pnl", precision = 18, scale = 2) private BigDecimal unrealizedPnl;
    @Column(name = "updated_at") private Instant updatedAt = Instant.now();
    @PrePersist public void prePersist() { if (id == null) id = UUID.randomUUID(); }
}
