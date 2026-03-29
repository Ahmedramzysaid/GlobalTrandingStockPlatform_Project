package com.gstp.settlement.service;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.gstp.settlement.model.Settlement;
import com.gstp.settlement.repository.SettlementRepository;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.kafka.annotation.KafkaListener;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.math.BigDecimal;
import java.time.Instant;
import java.time.LocalDate;
import java.util.List;
import java.util.UUID;

@Slf4j @Service @RequiredArgsConstructor
public class SettlementService {
    private final SettlementRepository settlementRepo;
    private final KafkaTemplate<String, String> kafkaTemplate;
    private final ObjectMapper mapper = new ObjectMapper();

    @KafkaListener(topics = "trades.executed", groupId = "settlement-service")
    public void handleTradeExecuted(String message) {
        try {
            JsonNode trade = mapper.readTree(message);
            BigDecimal qty = new BigDecimal(trade.get("Quantity").asText());
            BigDecimal price = new BigDecimal(trade.get("Price").asText());

            Settlement settlement = Settlement.builder()
                    .tradeId(UUID.fromString(trade.get("TradeId").asText()))
                    .buyerUserId(UUID.fromString(trade.get("BuyerUserId").asText()))
                    .sellerUserId(UUID.fromString(trade.get("SellerUserId").asText()))
                    .symbol(trade.get("Symbol").asText())
                    .quantity(qty)
                    .price(price)
                    .totalAmount(qty.multiply(price))
                    .status("PENDING")
                    .settlementDate(LocalDate.now().plusDays(2)) // T+2 settlement
                    .build();

            settlementRepo.save(settlement);
            log.info("Settlement created for trade {} — settles on {}", settlement.getTradeId(), settlement.getSettlementDate());
        } catch (Exception e) {
            log.error("Error creating settlement: {}", e.getMessage(), e);
        }
    }

    /** Process settlements that are due (T+2 reached) — runs every hour */
    @Scheduled(fixedRate = 3600000)
    @Transactional
    public void processSettlements() {
        List<Settlement> dueSettlements = settlementRepo.findByStatusAndSettlementDateLessThanEqual("PENDING", LocalDate.now());
        for (Settlement s : dueSettlements) {
            s.setStatus("SETTLED");
            s.setSettledAt(Instant.now());
            settlementRepo.save(s);

            try {
                kafkaTemplate.send("settlement.events", s.getSymbol(), mapper.writeValueAsString(s));
            } catch (Exception e) {
                log.error("Failed to publish settlement event", e);
            }
            log.info("Settlement {} processed for trade {}", s.getId(), s.getTradeId());
        }
        if (!dueSettlements.isEmpty()) log.info("Processed {} settlements", dueSettlements.size());
    }
}
