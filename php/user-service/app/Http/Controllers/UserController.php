<?php

namespace App\Http\Controllers;

use App\Models\User;
use App\Models\UserPreference;
use App\Models\Watchlist;
use Illuminate\Http\Request;
use Illuminate\Http\JsonResponse;
use Illuminate\Routing\Controller;

class UserController extends Controller
{
    /** GET /api/v1/users/health */
    public function health(): JsonResponse
    {
        return response()->json(['service' => 'User Service', 'status' => 'running']);
    }

    /** GET /api/v1/users/{id} */
    public function show(string $id): JsonResponse
    {
        $user = User::with(['preferences', 'watchlists'])->find($id);
        if (!$user) {
            return response()->json(['success' => false, 'error' => 'User not found'], 404);
        }
        return response()->json(['success' => true, 'data' => $user]);
    }

    /** POST /api/v1/users */
    public function store(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'auth_id' => 'required|uuid|unique:users,auth_id',
            'first_name' => 'required|string|max:100',
            'last_name' => 'required|string|max:100',
            'email' => 'required|email|unique:users,email',
            'phone' => 'nullable|string|max:20',
            'date_of_birth' => 'nullable|date',
            'country' => 'nullable|string|max:2'
        ]);

        $user = User::create($validated);

        // Create default preferences
        UserPreference::create([
            'user_id' => $user->id,
            'theme' => 'dark',
            'notifications' => ['email' => true, 'push' => true, 'sms' => false],
            'default_order' => 'MARKET',
            'timezone' => 'America/New_York'
        ]);

        return response()->json(['success' => true, 'data' => $user->load('preferences')], 201);
    }

    /** PUT /api/v1/users/{id} */
    public function update(Request $request, string $id): JsonResponse
    {
        $user = User::find($id);
        if (!$user) return response()->json(['error' => 'User not found'], 404);

        $user->update($request->only([
            'first_name', 'last_name', 'phone', 'date_of_birth',
            'address_line1', 'address_line2', 'city', 'state', 'country', 'zip_code', 'profile_image'
        ]));

        return response()->json(['success' => true, 'data' => $user->fresh()]);
    }

    /** PUT /api/v1/users/{id}/preferences */
    public function updatePreferences(Request $request, string $id): JsonResponse
    {
        $pref = UserPreference::updateOrCreate(
            ['user_id' => $id],
            $request->only(['theme', 'notifications', 'default_order', 'timezone'])
        );
        return response()->json(['success' => true, 'data' => $pref]);
    }

    /** GET /api/v1/users/{id}/watchlists */
    public function getWatchlists(string $id): JsonResponse
    {
        $watchlists = Watchlist::where('user_id', $id)->get();
        return response()->json(['success' => true, 'data' => $watchlists]);
    }

    /** POST /api/v1/users/{id}/watchlists */
    public function createWatchlist(Request $request, string $id): JsonResponse
    {
        $validated = $request->validate([
            'name' => 'required|string|max:100',
            'symbols' => 'required|array',
            'symbols.*' => 'string|max:10'
        ]);

        $watchlist = Watchlist::create(['user_id' => $id, ...$validated]);
        return response()->json(['success' => true, 'data' => $watchlist], 201);
    }

    /** PUT /api/v1/users/watchlists/{watchlistId} */
    public function updateWatchlist(Request $request, string $watchlistId): JsonResponse
    {
        $watchlist = Watchlist::find($watchlistId);
        if (!$watchlist) return response()->json(['error' => 'Not found'], 404);
        $watchlist->update($request->only(['name', 'symbols']));
        return response()->json(['success' => true, 'data' => $watchlist]);
    }

    /** DELETE /api/v1/users/watchlists/{watchlistId} */
    public function deleteWatchlist(string $watchlistId): JsonResponse
    {
        Watchlist::destroy($watchlistId);
        return response()->json(['success' => true, 'message' => 'Watchlist deleted']);
    }

    /** PUT /api/v1/users/{id}/kyc */
    public function updateKyc(Request $request, string $id): JsonResponse
    {
        $user = User::find($id);
        if (!$user) return response()->json(['error' => 'Not found'], 404);
        $user->update(['kyc_status' => $request->input('status', 'VERIFIED')]);
        return response()->json(['success' => true, 'data' => ['kyc_status' => $user->kyc_status]]);
    }
}
