FROM php:8.3-fpm-alpine
WORKDIR /app
RUN apk add --no-cache postgresql-dev && docker-php-ext-install pdo pdo_pgsql
COPY composer.json ./
RUN curl -sS https://getcomposer.org/installer | php -- --install-dir=/usr/local/bin --filename=composer
RUN composer install --no-scripts --no-autoloader 2>/dev/null || true
COPY . .
RUN composer dump-autoload 2>/dev/null || true
EXPOSE 8001
CMD ["php", "-S", "0.0.0.0:8001", "-t", "public"]
