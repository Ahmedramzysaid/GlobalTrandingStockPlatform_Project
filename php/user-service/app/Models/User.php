<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Concerns\HasUuids;

class User extends Model
{
    use HasUuids;

    protected $table = 'users';
    protected $keyType = 'string';
    public $incrementing = false;

    protected $fillable = [
        'auth_id', 'first_name', 'last_name', 'email', 'phone',
        'date_of_birth', 'address_line1', 'address_line2', 'city',
        'state', 'country', 'zip_code', 'ssn_encrypted', 'kyc_status',
        'account_tier', 'profile_image'
    ];

    protected $hidden = ['ssn_encrypted'];

    protected $casts = [
        'date_of_birth' => 'date',
    ];

    public function preferences()
    {
        return $this->hasOne(UserPreference::class);
    }

    public function watchlists()
    {
        return $this->hasMany(Watchlist::class);
    }
}

class UserPreference extends Model
{
    use HasUuids;

    protected $table = 'user_preferences';
    protected $keyType = 'string';
    public $incrementing = false;

    protected $fillable = ['user_id', 'theme', 'notifications', 'default_order', 'timezone'];

    protected $casts = [
        'notifications' => 'array',
    ];

    public function user()
    {
        return $this->belongsTo(User::class);
    }
}

class Watchlist extends Model
{
    use HasUuids;

    protected $table = 'watchlists';
    protected $keyType = 'string';
    public $incrementing = false;

    protected $fillable = ['user_id', 'name', 'symbols'];

    protected $casts = [
        'symbols' => 'array',
    ];

    public function user()
    {
        return $this->belongsTo(User::class);
    }
}
