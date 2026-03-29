package com.gstp.risk.model;

import jakarta.persistence.*;
import lombok.*;
import java.time.Instant;
import java.util.UUID;

@Entity @Table(name = "risk_alerts") @Data @NoArgsConstructor @AllArgsConstructor @Builder
public class RiskAlert {
    @Id private UUID id;
    @Column(name = "user_id") private UUID userId;
    @Column(name = "alert_type", nullable = false, length = 50) private String alertType;
    @Column(length = 20, nullable = false) private String severity;
    @Column(nullable = false, columnDefinition = "TEXT") private String message;
    @Column private boolean resolved = false;
    @Column(name = "created_at") private Instant createdAt = Instant.now();
    @PrePersist public void pp() { if (id == null) id = UUID.randomUUID(); }
}
