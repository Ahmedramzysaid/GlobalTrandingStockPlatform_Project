package com.gstp.settlement.controller;
import com.gstp.settlement.repository.SettlementRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;
import java.util.Map;

@RestController @RequestMapping("/api/v1/settlement") @RequiredArgsConstructor
public class SettlementController {
    private final SettlementRepository repo;
    @GetMapping("/pending") public ResponseEntity<?> getPending() { return ResponseEntity.ok(Map.of("success", true, "data", repo.findByStatusAndSettlementDateLessThanEqual("PENDING", java.time.LocalDate.now().plusDays(7)))); }
    @GetMapping("/health") public ResponseEntity<?> health() { return ResponseEntity.ok(Map.of("service", "Settlement Service", "status", "running")); }
}
