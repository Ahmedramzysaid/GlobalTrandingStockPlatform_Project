package com.gstp.trading.repository;

import com.gstp.trading.model.Trade;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.UUID;

@Repository
public interface TradeRepository extends JpaRepository<Trade, UUID> {
    List<Trade> findBySymbolOrderByExecutedAtDesc(String symbol);
    List<Trade> findByBuyerUserIdOrSellerUserIdOrderByExecutedAtDesc(UUID buyerId, UUID sellerId);
}
