import { useSyncExternalStore } from 'react'
import {
  clearCompared,
  compareLimit,
  getCompared,
  subscribeToCompared,
  toggleCompared,
} from './compareStore'

interface CompareControls {
  /** Номери відкладених оголошень, у порядку додавання. */
  ids: number[]

  /** Чи можна додати ще. */
  hasRoom: boolean

  isCompared: (listingId: number) => boolean

  /** Повертає false, якщо додати не вдалося через межу. */
  toggle: (listingId: number) => boolean

  clear: () => void
}

/**
 * Доступ до списку порівняння.
 *
 * Через useSyncExternalStore, як і тема: сховище живе поза React, тож
 * кнопка на картці, лічильник у шапці й сама таблиця бачать одне значення
 * без жодного провайдера.
 */
export function useCompare(): CompareControls {
  const ids = useSyncExternalStore(subscribeToCompared, getCompared)

  return {
    ids,
    hasRoom: ids.length < compareLimit,
    isCompared: (listingId) => ids.includes(listingId),
    toggle: toggleCompared,
    clear: clearCompared,
  }
}
