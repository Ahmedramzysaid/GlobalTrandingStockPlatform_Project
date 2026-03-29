package com.gstp.portfolio.controller;

import com.gstp.portfolio.repository.PortfolioRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;
import java.util.Map;
import java.util.UUID;

@RestController @RequestMapping("/api/v1/portfolio") @RequiredArgsConstructor
public class PortfolioController {
    private final PortfolioRepository portfolioRepo;

    @GetMapping("/{userId}")
    public ResponseEntity<?> getPortfolio(@PathVariable UUID userId) {
        return portfolioRepo.findByUserId(userId)
                .map(p -> ResponseEntity.ok(Map.of("success", true, "data", p)))
                .orElse(ResponseEntity.notFound().build());
    }

    @GetMapping("/health")
    public ResponseEntity<?> health() {
        return ResponseEntity.ok(Map.of("service", "Portfolio Service", "status", "running"));
    }
}
