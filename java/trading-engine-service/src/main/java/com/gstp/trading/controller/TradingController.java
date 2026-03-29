package com.gstp.trading.controller;

import com.gstp.trading.engine.MatchingEngine;
import com.gstp.trading.repository.TradeRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

import java.util.Map;

@RestController
@RequestMapping("/api/v1/trading")
@RequiredArgsConstructor
public class TradingController {

    private final MatchingEngine matchingEngine;
    private final TradeRepository tradeRepository;

    @GetMapping("/orderbook/{symbol}")
    public ResponseEntity<?> getOrderBook(@PathVariable String symbol,
                                           @RequestParam(defaultValue = "10") int depth) {
        return ResponseEntity.ok(Map.of(
                "success", true,
                "data", matchingEngine.getOrderBookSnapshot(symbol.toUpperCase(), depth)
        ));
    }

    @GetMapping("/trades/{symbol}")
    public ResponseEntity<?> getRecentTrades(@PathVariable String symbol) {
        var trades = tradeRepository.findBySymbolOrderByExecutedAtDesc(symbol.toUpperCase());
        return ResponseEntity.ok(Map.of("success", true, "data", trades));
    }

    @GetMapping("/health")
    public ResponseEntity<?> health() {
        return ResponseEntity.ok(Map.of("service", "Trading Engine", "status", "running"));
    }
}
