package com.gstp.portfolio.repository;
import com.gstp.portfolio.model.Holding;
import org.springframework.data.jpa.repository.JpaRepository;
import java.util.Optional;
import java.util.UUID;

public interface HoldingRepository extends JpaRepository<Holding, UUID> {
    Optional<Holding> findByPortfolioIdAndSymbol(UUID portfolioId, String symbol);
}
