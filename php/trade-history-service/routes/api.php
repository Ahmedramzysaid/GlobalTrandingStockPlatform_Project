<?php
use App\Http\Controllers\TradeHistoryController;
use Illuminate\Support\Facades\Route;

Route::prefix('api/v1/history')->group(function () {
    Route::get('/health', [TradeHistoryController::class, 'health']);
    Route::get('/{userId}', [TradeHistoryController::class, 'getUserHistory']);
    Route::get('/{userId}/summary', [TradeHistoryController::class, 'getUserSummary']);
    Route::get('/{userId}/pnl', [TradeHistoryController::class, 'getUserPnl']);
});
