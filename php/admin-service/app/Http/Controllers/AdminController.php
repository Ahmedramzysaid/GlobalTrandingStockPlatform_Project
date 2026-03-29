<?php

namespace App\Http\Controllers;

use Illuminate\Http\Request;
use Illuminate\Http\JsonResponse;
use Illuminate\Routing\Controller;
use Illuminate\Support\Facades\DB;

class AdminController extends Controller
{
    public function health(): JsonResponse
    {
        return response()->json(['service' => 'Admin Service', 'status' => 'running']);
    }

    /** GET /api/v1/admin/dashboard */
    public function dashboard(): JsonResponse
    {
        return response()->json([
            'success' => true,
            'data' => [
                'totalUsers' => DB::connection('admin')->table('system_configs')->count(),
                'tradingEnabled' => $this->getConfig('trading_enabled'),
                'marketHours' => [
                    'start' => $this->getConfig('market_hours_start'),
                    'end' => $this->getConfig('market_hours_end')
                ],
                'timestamp' => now()
            ]
        ]);
    }

    /** GET /api/v1/admin/configs */
    public function getConfigs(): JsonResponse
    {
        $configs = DB::connection('admin')->table('system_configs')->get();
        return response()->json(['success' => true, 'data' => $configs]);
    }

    /** PUT /api/v1/admin/configs/{key} */
    public function updateConfig(Request $request, string $key): JsonResponse
    {
        $validated = $request->validate(['value' => 'required|string']);

        DB::connection('admin')->table('system_configs')
            ->where('key', $key)
            ->update([
                'value' => $validated['value'],
                'updated_at' => now()
            ]);

        return response()->json(['success' => true, 'message' => "Config '{$key}' updated"]);
    }

    /** POST /api/v1/admin/trading/toggle */
    public function toggleTrading(): JsonResponse
    {
        $current = $this->getConfig('trading_enabled');
        $newValue = $current === 'true' ? 'false' : 'true';

        DB::connection('admin')->table('system_configs')
            ->where('key', 'trading_enabled')
            ->update(['value' => $newValue, 'updated_at' => now()]);

        return response()->json([
            'success' => true,
            'data' => ['trading_enabled' => $newValue === 'true']
        ]);
    }

    /** GET /api/v1/admin/announcements */
    public function getAnnouncements(): JsonResponse
    {
        $announcements = DB::connection('admin')->table('announcements')
            ->where('is_active', true)
            ->orderByDesc('created_at')
            ->get();
        return response()->json(['success' => true, 'data' => $announcements]);
    }

    /** POST /api/v1/admin/announcements */
    public function createAnnouncement(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'title' => 'required|string|max:255',
            'content' => 'required|string',
            'expires_at' => 'nullable|date'
        ]);

        $id = DB::connection('admin')->table('announcements')->insertGetId([
            'id' => \Illuminate\Support\Str::uuid(),
            'title' => $validated['title'],
            'content' => $validated['content'],
            'expires_at' => $validated['expires_at'] ?? null,
            'created_at' => now()
        ]);

        return response()->json(['success' => true, 'message' => 'Announcement created'], 201);
    }

    private function getConfig(string $key): ?string
    {
        return DB::connection('admin')->table('system_configs')
            ->where('key', $key)->value('value');
    }
}
