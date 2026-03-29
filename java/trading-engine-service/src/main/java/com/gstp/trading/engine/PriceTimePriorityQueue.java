package com.gstp.trading.engine;

import com.gstp.trading.model.Order;
import lombok.Getter;
import java.math.BigDecimal;
import java.util.*;

/**
 * Price-Time Priority Queue for one side of the order book.
 * BUY side: highest price first (descending).
 * SELL side: lowest price first (ascending).
 */
@Getter
public class PriceTimePriorityQueue {
    private final TreeMap<BigDecimal, LinkedList<Order>> levels;
    private final Order.OrderSide side;

    public PriceTimePriorityQueue(Order.OrderSide side) {
        this.side = side;
        this.levels = (side == Order.OrderSide.BUY)
                ? new TreeMap<>(Comparator.reverseOrder())
                : new TreeMap<>();
    }

    public void addOrder(Order order) {
        levels.computeIfAbsent(order.getPrice(), k -> new LinkedList<>())
              .addLast(order);
    }

    public Order peekBest() {
        Map.Entry<BigDecimal, LinkedList<Order>> best = levels.firstEntry();
        return (best != null && !best.getValue().isEmpty())
                ? best.getValue().peekFirst() : null;
    }

    public Order pollBest() {
        Map.Entry<BigDecimal, LinkedList<Order>> best = levels.firstEntry();
        if (best == null) return null;
        Order order = best.getValue().pollFirst();
        if (best.getValue().isEmpty()) levels.remove(best.getKey());
        return order;
    }

    public boolean removeOrder(UUID orderId) {
        for (var entry : levels.entrySet()) {
            if (entry.getValue().removeIf(o -> o.getId().equals(orderId))) {
                if (entry.getValue().isEmpty()) levels.remove(entry.getKey());
                return true;
            }
        }
        return false;
    }

    public int size() {
        return levels.values().stream().mapToInt(LinkedList::size).sum();
    }

    /** Get top N price levels for order book display */
    public List<Map<String, Object>> getTopLevels(int n) {
        List<Map<String, Object>> result = new ArrayList<>();
        int count = 0;
        for (var entry : levels.entrySet()) {
            if (count >= n) break;
            BigDecimal totalQty = entry.getValue().stream()
                    .map(Order::getRemainingQuantity)
                    .reduce(BigDecimal.ZERO, BigDecimal::add);
            result.add(Map.of(
                    "price", entry.getKey(),
                    "quantity", totalQty,
                    "orders", entry.getValue().size()
            ));
            count++;
        }
        return result;
    }
}
