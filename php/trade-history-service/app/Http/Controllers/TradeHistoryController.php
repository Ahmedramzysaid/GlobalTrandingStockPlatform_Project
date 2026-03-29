<?php

namespace App\Http\Controllers;

use Illuminate\Http\Request;
use Illuminate\Http\JsonResponse;
use Illuminate\Routing\Controller;
use Illuminate\Support\Facades\DB;

class TradeHistoryController extends Controller
{
    public function health(): JsonResponse
    {
        return response()->json(['service' => 'Trade History Service', 'status' => 'running']);
    }

    /** GET /api/v1/history/{userId} */
    public function getUserHistory(Request $request, string $userId): JsonResponse
    {
        $page = $request->query('page', 1);
        $limit = $request->query('limit', 20);
        $symbol = $request->query('symbol');
        $side = $request->query('side');
        $from = $request->query('from');
        $to = $request->query('to');

        $query = DB::table('trade_records')->where('user_id', $userId);

        if ($symbol) $query->where('symbol', strtoupper($symbol));
        if ($side) $query->where('side', strtoupper($side));
        if ($from) $query->where('executed_at', '>=', $from);
        if ($to) $query->where('executed_at', '<=', $to);

        $total = $query->count();
        $records = $query->orderByDesc('executed_at')
            ->offset(($page - 1) * $limit)
            ->limit($limit)
            ->get();

        return response()->json([
            'success' => true,
            'data' => $records,
            'pagination' => [
                'page' => (int)$page,
                'limit' => (int)$limit,
                'total' => $total,
                'totalPages' => ceil($total / $limit)
            ]
        ]);
    }

    /** GET /api/v1/history/{userId}/summary */
    public function getUserSummary(string $userId): JsonResponse
    {
        $summary = DB::table('trade_records')
            ->where('user_id', $userId)
            ->selectRaw('
                COUNT(*) as total_trades,
                SUM(total_amount) as total_volume,
                SUM(commission) as total_commissions,
                MIN(executed_at) as first_trade,
                MAX(executed_at) as last_trade
            ')
            ->first();

        $topSymbols = DB::table('trade_records')
            ->where('user_id', $userId)
            ->groupBy('symbol')
            ->selectRaw('symbol, COUNT(*) as trade_count, SUM(total_amount) as volume')
            ->orderByDesc('trade_count')
            ->limit(10)
            ->get();

        return response()->json([
            'success' => true,
            'data' => [
                'summary' => $summary,
                'topSymbols' => $topSymbols
            ]
        ]);
    }

    /** GET /api/v1/history/{userId}/pnl */
    public function getUserPnl(Request $request, string $userId): JsonResponse
    {
        $buys = DB::table('trade_records')
            ->where('user_id', $userId)
            ->where('side', 'BUY')
            ->selectRaw('symbol, SUM(quantity) as total_qty, SUM(total_amount) as total_cost')
            ->groupBy('symbol')
            ->get();

        $sells = DB::table('trade_records')
            ->where('user_id', $userId)
            ->where('side', 'SELL')
            ->selectRaw('symbol, SUM(quantity) as total_qty, SUM(total_amount) as total_revenue')
            ->groupBy('symbol')
            ->get();

        return response()->json([
            'success' => true,
            'data' => ['buys' => $buys, 'sells' => $sells]
        ]);
    }
}
