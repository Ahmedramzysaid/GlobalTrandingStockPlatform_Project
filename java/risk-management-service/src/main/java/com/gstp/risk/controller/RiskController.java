package com.gstp.risk.controller;

import com.gstp.risk.repository.RiskAlertRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;
import java.util.Map;

@RestController @RequestMapping("/api/v1/risk") @RequiredArgsConstructor
public class RiskController {
    private final RiskAlertRepository alertRepo;

    @GetMapping("/alerts")
    public ResponseEntity<?> getAlerts() {
        return ResponseEntity.ok(Map.of("success", true, "data", alertRepo.findAll()));
    }

    @GetMapping("/health")
    public ResponseEntity<?> health() {
        return ResponseEntity.ok(Map.of("service", "Risk Management", "status", "running"));
    }
}
