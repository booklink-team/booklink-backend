#!/usr/bin/env bash
# Проверка доступа и целостности на живом сервере с демо-наполнением (issue #12).
# Использование: scripts/security-check.sh http://localhost:8080 <пароль демо-учёток>
set -euo pipefail
API=$1 PASS=$2 N=10
fail() { echo "FAIL: $*"; exit 1; }
code() { curl -s -o /dev/null -w '%{http_code}' "$@"; }
login() { curl -sf "$API/api/auth/login" -H 'Content-Type: application/json' \
  -d "{\"login\":\"$1\",\"password\":\"$PASS\"}" | jq -r .accessToken; }
S1=$(login student1) S2=$(login student2) L=$(login librarian)
auth() { echo "Authorization: Bearer $1"; }
# N параллельных запросов; печатает коды ответов.
burst() { for _ in $(seq $N); do curl -s -o /dev/null -w '%{http_code}\n' "$@" & done; wait; }

echo "== Доступ"
[ "$(code "$API/api/books")" = 401 ] || fail "каталог без токена"
[ "$(code -H "$(auth "$S1")" "$API/api/librarian/reservations")" = 403 ] || fail "студент открыл API библиотекаря"
[ "$(code -H "$(auth "$L")" "$API/api/reservations/my")" = 403 ] || fail "библиотекарь открыл API студента"
[ "$(code -H "$(auth "$S1")" "$API/api/books?page=2147483647")" = 200 ] || fail "огромный page"

BOOK=$(curl -sf -H "$(auth "$S1")" "$API/api/books?search=%D0%9E%D0%B1%D0%BB%D0%BE%D0%BC%D0%BE%D0%B2" | jq '.items[0].id')

echo "== Последний экземпляр: $((2 * N)) одновременных броней от двух студентов"
codes=$( (burst -X POST "$API/api/reservations" -H "$(auth "$S1")" -H 'Content-Type: application/json' -d "{\"bookId\":$BOOK}" &
          burst -X POST "$API/api/reservations" -H "$(auth "$S2")" -H 'Content-Type: application/json' -d "{\"bookId\":$BOOK}"; wait) )
[ "$(grep -c '^200$' <<<"$codes")" = 1 ] || fail "успешных броней не одна: $(sort <<<"$codes" | uniq -c)"
grep -q '^5' <<<"$codes" && fail "ошибка 5xx при гонке броней"
RES=$(curl -sf -H "$(auth "$L")" "$API/api/librarian/reservations" | jq --argjson b "$BOOK" '[.[] | select(.book.id == $b)] | .[0].id')
OWNER=$(curl -sf -H "$(auth "$L")" "$API/api/librarian/reservations/$RES" | jq -r .reader.login)
TOKEN=$([ "$OWNER" = student1 ] && echo "$S1" || echo "$S2")
OTHER=$([ "$OWNER" = student1 ] && echo "$S2" || echo "$S1")

echo "== Чужая бронь"
[ "$(code -X POST -H "$(auth "$OTHER")" "$API/api/reservations/$RES/cancel")" = 404 ] || fail "отмена чужой брони"

echo "== $N одновременных подтверждений выдачи"
codes=$(burst -X POST -H "$(auth "$L")" "$API/api/librarian/reservations/$RES/issue")
grep -qv '^200$' <<<"$codes" && fail "выдача: $(sort <<<"$codes" | uniq -c)"
LOANS=$(curl -sf -H "$(auth "$TOKEN")" "$API/api/loans/my" | jq --argjson b "$BOOK" '[.[] | select(.book.id == $b)]')
[ "$(jq length <<<"$LOANS")" = 1 ] || fail "создано выдач: $(jq length <<<"$LOANS")"
LOAN=$(jq '.[0].id' <<<"$LOANS") DUE=$(jq -r '.[0].dueAt' <<<"$LOANS")

echo "== Чужая выдача"
[ "$(code -X POST -H "$(auth "$OTHER")" "$API/api/loans/$LOAN/renew")" = 404 ] || fail "продление чужой выдачи"

echo "== $N одновременных продлений"
codes=$(burst -X POST -H "$(auth "$TOKEN")" "$API/api/loans/$LOAN/renew")
[ "$(grep -c '^200$' <<<"$codes")" = 1 ] || fail "успешных продлений не одно: $(sort <<<"$codes" | uniq -c)"
NEW_DUE=$(curl -sf -H "$(auth "$L")" "$API/api/librarian/loans/$LOAN" | jq -r .dueAt)
secs() { jq -n --arg d "$1" '$d | sub("\\.[0-9]+"; "") | fromdateiso8601'; }
[ $(( $(secs "$NEW_DUE") - $(secs "$DUE") )) = $((7 * 86400)) ] || fail "срок $DUE → $NEW_DUE"

echo "== $N одновременных возвратов"
codes=$(burst -X POST -H "$(auth "$L")" "$API/api/librarian/loans/$LOAN/return")
grep -qv '^200$' <<<"$codes" && fail "возврат: $(sort <<<"$codes" | uniq -c)"
[ "$(curl -sf -H "$(auth "$S1")" "$API/api/books/$BOOK" | jq .availableCopies)" = 1 ] || fail "экземпляр не освободился"

echo "== $N одновременных списков с одним названием"
codes=$(burst -X POST "$API/api/lists" -H "$(auth "$S1")" -H 'Content-Type: application/json' -d '{"name":"Гонка"}')
[ "$(grep -c '^200$' <<<"$codes")" = 1 ] || fail "создано списков: $(sort <<<"$codes" | uniq -c)"
grep -q '^5' <<<"$codes" && fail "ошибка 5xx при гонке списков"

echo "== Перебор пароля"
for _ in $(seq 15); do
  c=$(code "$API/api/auth/login" -H 'Content-Type: application/json' -d '{"login":"student1","password":"wrong"}')
  [ "$c" = 429 ] && { echo "OK: все проверки пройдены"; exit 0; }
done
fail "нет ограничения попыток входа"
