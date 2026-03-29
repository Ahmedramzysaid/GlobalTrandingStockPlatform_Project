<?php
use App\Http\Controllers\AdminController;
use Illuminate\Support\Facades\Route;

Route::prefix('api/v1/admin')->group(function () {
    Route::get('/health', [AdminController::class, 'health']);
    Route::get('/dashboard', [AdminController::class, 'dashboard']);
    Route::get('/configs', [AdminController::class, 'getConfigs']);
    Route::put('/configs/{key}', [AdminController::class, 'updateConfig']);
    Route::post('/trading/toggle', [AdminController::class, 'toggleTrading']);
    Route::get('/announcements', [AdminController::class, 'getAnnouncements']);
    Route::post('/announcements', [AdminController::class, 'createAnnouncement']);
});
