<?php

use App\Http\Controllers\UserController;
use Illuminate\Support\Facades\Route;

Route::prefix('api/v1/users')->group(function () {
    Route::get('/health', [UserController::class, 'health']);
    Route::get('/{id}', [UserController::class, 'show']);
    Route::post('/', [UserController::class, 'store']);
    Route::put('/{id}', [UserController::class, 'update']);
    Route::put('/{id}/preferences', [UserController::class, 'updatePreferences']);
    Route::put('/{id}/kyc', [UserController::class, 'updateKyc']);
    Route::get('/{id}/watchlists', [UserController::class, 'getWatchlists']);
    Route::post('/{id}/watchlists', [UserController::class, 'createWatchlist']);
    Route::put('/watchlists/{watchlistId}', [UserController::class, 'updateWatchlist']);
    Route::delete('/watchlists/{watchlistId}', [UserController::class, 'deleteWatchlist']);
});
