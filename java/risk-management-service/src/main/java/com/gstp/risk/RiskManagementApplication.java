package com.gstp.risk;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.kafka.annotation.EnableKafka;

@SpringBootApplication @EnableKafka
public class RiskManagementApplication {
    public static void main(String[] args) { SpringApplication.run(RiskManagementApplication.class, args); }
}
