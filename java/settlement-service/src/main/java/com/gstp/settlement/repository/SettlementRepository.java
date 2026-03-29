package com.gstp.settlement.repository;
import com.gstp.settlement.model.Settlement;
import org.springframework.data.jpa.repository.JpaRepository;
import java.time.LocalDate;
import java.util.List;
import java.util.UUID;

public interface SettlementRepository extends JpaRepository<Settlement, UUID> {
    List<Settlement> findByStatusAndSettlementDateLessThanEqual(String status, LocalDate date);
}
