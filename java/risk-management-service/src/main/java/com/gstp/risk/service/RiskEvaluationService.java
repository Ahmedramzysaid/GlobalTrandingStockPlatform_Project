package com.gstp.risk.service;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.gstp.risk.model.RiskAlert;
import com.gstp.risk.repository.RiskAlertRepository;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.kafka.annotation.KafkaListener;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.stereotype.Service;

import java.math.BigDecimal;
import java.util.UUID;

@Slf4j @Service @RequiredArgsConstructor
public class RiskEvaluationService {
    private final RiskAlertRepository alertRepo;
    private final KafkaTemplate<String, String> kafkaTemplate;
    private final ObjectMapper mapper = new ObjectMapper();

    private static final BigDecimal MAX_ORDER_VALUE = new BigDecimal("1000000");
    private static final BigDecimal MAX_SINGLE_TRADE = new BigDecimal("100000");

    @KafkaListener(topics = "orders.created", groupId = "risk-management")
    public void evaluateOrderRisk(String message) {
        try {
            JsonNode root = mapper.readTree(message);
            JsonNode data = root.get("Data");
            BigDecimal qty = new BigDecimal(data.get("Quantity").asText());
            BigDecimal price = data.has("Price") && !data.get("Price").isNull()
                    ? new BigDecimal(data.get("Price").asText()) : BigDecimal.ZERO;
            BigDecimal orderValue = qty.multiply(price);
            UUID userId = UUID.fromString(data.get("UserId").asText());
            String symbol = data.get("Symbol").asText();

            // Check single order value limit
            if (orderValue.compareTo(MAX_ORDER_VALUE) > 0) {
                createAlert(userId, "EXCESSIVE_ORDER_VALUE", "HIGH",
                        String.format("Order value $%s exceeds limit $%s for %s", orderValue, MAX_ORDER_VALUE, symbol));
            }

            // Check large single trade
            if (qty.compareTo(MAX_SINGLE_TRADE) > 0) {
                createAlert(userId, "LARGE_QUANTITY", "MEDIUM",
                        String.format("Order quantity %s exceeds threshold for %s", qty, symbol));
            }

            log.info("Risk evaluation complete for order on {}", symbol);
        } catch (Exception e) {
            log.error("Risk evaluation error: {}", e.getMessage(), e);
        }
    }

    private void createAlert(UUID userId, String type, String severity, String message) {
        RiskAlert alert = RiskAlert.builder()
                .userId(userId).alertType(type).severity(severity).message(message).build();
        alertRepo.save(alert);
        try {
            kafkaTemplate.send("risk.alerts", userId.toString(), mapper.writeValueAsString(alert));
        } catch (Exception e) {
            log.error("Failed to publish risk alert", e);
        }
        log.warn("RISK ALERT [{}]: {}", severity, message);
    }
}
