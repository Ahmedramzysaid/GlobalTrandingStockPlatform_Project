package com.gstp.portfolio.model;

import jakarta.persistence.*;
import lombok.*;
import java.math.BigDecimal;
import java.time.Instant;
import java.util.*;

@Entity @Table(name = "portfolios")
@Data @NoArgsConstructor @AllArgsConstructor @Builder
public class Portfolio {
    @Id private UUID id;
    @Column(name = "user_id", unique = true, nullable = false) private UUID userId;
    @Column(name = "total_value", precision = 18, scale = 2) private BigDecimal totalValue = BigDecimal.ZERO;
    @Column(name = "total_cost", precision = 18, scale = 2) private BigDecimal totalCost = BigDecimal.ZERO;
    @Column(name = "realized_pnl", precision = 18, scale = 2) private BigDecimal realizedPnl = BigDecimal.ZERO;
    @Column(name = "updated_at") private Instant updatedAt = Instant.now();
    @OneToMany(mappedBy = "portfolio", cascade = CascadeType.ALL, fetch = FetchType.EAGER)
    private List<Holding> holdings = new ArrayList<>();
    @PrePersist public void prePersist() { if (id == null) id = UUID.randomUUID(); }
}
