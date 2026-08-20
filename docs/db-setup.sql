-- Створює роль і базу для локальної розробки.
-- Запускати від суперкористувача:
--   psql -h localhost -p 5433 -U postgres -f docs/db-setup.sql -v password='<пароль>'
--
-- Пароль у репозиторій не потрапляє: він передається параметром і далі
-- зберігається лише в dotnet user-secrets.

\set ON_ERROR_STOP on

SELECT format('CREATE ROLE autolot LOGIN PASSWORD %L', :'password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'autolot')
\gexec

SELECT 'CREATE DATABASE autolot OWNER autolot ENCODING ''UTF8'''
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'autolot')
\gexec

\connect autolot

GRANT ALL ON SCHEMA public TO autolot;
ALTER SCHEMA public OWNER TO autolot;
