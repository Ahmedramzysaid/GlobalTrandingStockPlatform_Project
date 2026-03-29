package com.gstp.risk.repository;
import com.gstp.risk.model.RiskAlert;
import org.springframework.data.jpa.repository.JpaRepository;
import java.util.UUID;

public interface RiskAlertRepository extends JpaRepository<RiskAlert, UUID> {}
