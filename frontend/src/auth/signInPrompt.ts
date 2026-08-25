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

const listeners = new Set<() => void>()

function notify(): void {
  listeners.forEach((listener) => listener())
}

export function openSignIn(): void {
  isOpen = true
  notify()
}

export function closeSignIn(): void {
  isOpen = false
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
