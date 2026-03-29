package com.gstp.trading;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.kafka.annotation.EnableKafka;

@SpringBootApplication
@EnableKafka
public class TradingEngineApplication {
    public static void main(String[] args) {
        SpringApplication.run(TradingEngineApplication.class, args);
    }
}
