package com.gstp.portfolio.kafka;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.gstp.portfolio.model.Holding;
import com.gstp.portfolio.model.Portfolio;
import com.gstp.portfolio.repository.HoldingRepository;
import com.gstp.portfolio.repository.PortfolioRepository;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.kafka.annotation.KafkaListener;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Transactional;

import java.math.BigDecimal;
import java.math.RoundingMode;
import java.time.Instant;
import java.util.UUID;

@Slf4j @Component @RequiredArgsConstructor
public class TradeConsumer {
    private final PortfolioRepository portfolioRepo;
    private final HoldingRepository holdingRepo;
    private final ObjectMapper mapper = new ObjectMapper();

    @Transactional
    @KafkaListener(topics = "trades.executed", groupId = "portfolio-service")
    public void handleTradeExecuted(String message) {
        try {
            JsonNode trade = mapper.readTree(message);
            UUID buyerUserId = UUID.fromString(trade.get("BuyerUserId").asText());
            UUID sellerUserId = UUID.fromString(trade.get("SellerUserId").asText());
            String symbol = trade.get("Symbol").asText();
            BigDecimal qty = new BigDecimal(trade.get("Quantity").asText());
            BigDecimal price = new BigDecimal(trade.get("Price").asText());

            // Update buyer portfolio (add holding)
            updatePortfolio(buyerUserId, symbol, qty, price, true);
            // Update seller portfolio (remove holding)
            updatePortfolio(sellerUserId, symbol, qty, price, false);

            log.info("Portfolio updated for trade: {} {} @ {}", symbol, qty, price);
        } catch (Exception e) {
            log.error("Error processing trade for portfolio: {}", e.getMessage(), e);
        }
    }

    private void updatePortfolio(UUID userId, String symbol, BigDecimal qty, BigDecimal price, boolean isBuy) {
        Portfolio portfolio = portfolioRepo.findByUserId(userId)
                .orElseGet(() -> portfolioRepo.save(Portfolio.builder().id(UUID.randomUUID()).userId(userId).build()));

        Holding holding = holdingRepo.findByPortfolioIdAndSymbol(portfolio.getId(), symbol)
                .orElseGet(() -> Holding.builder().portfolio(portfolio).symbol(symbol)
                        .quantity(BigDecimal.ZERO).avgCost(BigDecimal.ZERO).build());

        if (isBuy) {
            BigDecimal totalCost = holding.getAvgCost().multiply(holding.getQuantity()).add(price.multiply(qty));
            BigDecimal newQty = holding.getQuantity().add(qty);
            holding.setQuantity(newQty);
            holding.setAvgCost(newQty.compareTo(BigDecimal.ZERO) > 0 ? totalCost.divide(newQty, 8, RoundingMode.HALF_UP) : BigDecimal.ZERO);
        } else {
            holding.setQuantity(holding.getQuantity().subtract(qty));
        }
        holding.setCurrentPrice(price);
        holding.setUpdatedAt(Instant.now());
        holdingRepo.save(holding);

        portfolio.setUpdatedAt(Instant.now());
        portfolioRepo.save(portfolio);
    }
}
