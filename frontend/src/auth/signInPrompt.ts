import { useSyncExternalStore } from 'react'

/**
 * Прохання увійти. Вікно входу малює лише SiteLayout, але відкрити його
 * може будь-хто — наприклад, сердечко на картці, яке натиснув гість.
 *
 * Це те саме крихітне сховище поза React, що й у теми: інакше довелося б
 * протягувати функцію «відкрий вікно» через усі проміжні компоненти до
 * кожної кнопки, яка може її потребувати.
 */

let isOpen = false

/**
 * Чому вікно відкрилося саме зараз. Порожньо — людина натиснула сама;
 * заповнено — повернулася від Google із відмовою, і їй треба пояснити,
 * що сталося.
 */
let reason: string | null = null

const listeners = new Set<() => void>()

function notify(): void {
  listeners.forEach((listener) => listener())
}

export function openSignIn(): void {
  isOpen = true
  reason = null
  notify()
}

/** Відкрити вікно й одразу пояснити, чому попередня спроба не вдалася. */
export function openSignInWithReason(message: string): void {
  isOpen = true
  reason = message
  notify()
}

export function closeSignIn(): void {
  isOpen = false
  reason = null
  notify()
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener)

  return () => {
    listeners.delete(listener)
  }
}

/** Чи показувати вікно входу просто зараз. */
export function useSignInPrompt(): boolean {
  return useSyncExternalStore(subscribe, () => isOpen)
}

/** Причина, з якою вікно відкрилося. Порожньо, якщо його відкрили вручну. */
export function useSignInReason(): string | null {
  return useSyncExternalStore(subscribe, () => reason)
}
