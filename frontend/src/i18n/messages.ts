/**
 * Словники інтерфейсу. Українська — джерело істини: саме її ключі задають
 * тип, а англійська зобов'язана покрити їх усі.
 *
 * Ключі пласкі, з крапками всередині назви («nav.buy»). Плаский об'єкт
 * простіший за вкладений: ключ — звичайний рядок, тож TypeScript перевіряє
 * його одразу, без хитрощів із типами шляхів.
 *
 * Готової бібліотеки тут немає навмисно. i18next важить більше, ніж уся ця
 * тека, а дає те, чого проєкту не треба: підвантаження словників частинами,
 * десятки мов, відмінювання за числом у мовах із шістьма формами множини.
 * Двом мовам і пласкому словнику вистачає шістдесяти рядків свого коду —
 * і його видно наскрізь.
 */

const uk = {
  'nav.buy': 'Купити авто',
  'nav.compare': 'Порівняння',
  'nav.favorites': 'Обране',
  'nav.messages': 'Повідомлення',
  'nav.dealers': 'Автосалони',
  'nav.admin': 'Адмінка',
  'nav.auctions': 'Аукціони',

  'account.signIn': 'Увійти',
  'account.signOut': 'Вийти',
  'account.sell': 'Продати авто',
  'account.cabinet': '{name} — кабінет',

  'theme.toDark': 'Темна тема',
  'theme.toLight': 'Світла тема',
  'theme.enableDark': 'Увімкнути темну тему',
  'theme.enableLight': 'Увімкнути світлу тему',

  // Напис на кнопці — код мови, НА ЯКУ перемкне, а не поточної.
  'language.other': 'EN',
  'language.switch': 'Switch to English — перейти на англійську',

  'auth.tabSignIn': 'Вхід',
  'auth.tabRegister': 'Реєстрація',
  'auth.displayName': 'Як до вас звертатися',
  'auth.email': 'Email',
  'auth.password': 'Пароль',
  'auth.passwordHint': 'Щонайменше 8 символів, велика й мала літери та цифра',
  'auth.accountType': 'Тип акаунта',
  'auth.private': 'Приватна особа',
  'auth.dealer': 'Автосалон',
  'auth.busy': 'Хвилинку…',
  'auth.signIn': 'Увійти',
  'auth.register': 'Зареєструватися',
  'auth.forgot': 'Забули пароль?',
  'auth.resetTitle': 'Відновлення пароля',
  'auth.resetLead': 'Надішлемо посилання на вашу пошту.',
  'auth.resetSending': 'Надсилаємо…',
  'auth.resetSend': 'Надіслати посилання',
  'auth.resetRemembered': 'Згадав пароль',
  'auth.checkMail': 'Перевірте пошту',
  'auth.checkMailText':
    'Якщо {email} зареєстрована в AutoLot, ми надіслали туди посилання для зміни пароля.',
  'auth.backToSignIn': 'Повернутися до входу',

  'error.network': 'Не вдалося зв’язатися з сервером.',
  'error.requestFailed': 'Запит {path} завершився помилкою',
  'error.badResponse': 'Некоректна відповідь від {path}',
} as const

/** Назва ключа словника. Написати неіснуючий ключ TypeScript не дасть. */
export type MessageKey = keyof typeof uk

/**
 * Тип Record<MessageKey, string> — і є вся перевірка повноти перекладу:
 * забутий ключ стає помилкою збірки, а не порожнім місцем на екрані.
 */
const en: Record<MessageKey, string> = {
  'nav.buy': 'Buy a car',
  'nav.compare': 'Compare',
  'nav.favorites': 'Saved',
  'nav.messages': 'Messages',
  'nav.dealers': 'Dealerships',
  'nav.admin': 'Admin',
  'nav.auctions': 'Auctions',

  'account.signIn': 'Sign in',
  'account.signOut': 'Sign out',
  'account.sell': 'Sell a car',
  'account.cabinet': '{name} — account',

  'theme.toDark': 'Dark theme',
  'theme.toLight': 'Light theme',
  'theme.enableDark': 'Switch to the dark theme',
  'theme.enableLight': 'Switch to the light theme',

  'language.other': 'УКР',
  'language.switch': 'Перейти на українську — switch to Ukrainian',

  'auth.tabSignIn': 'Sign in',
  'auth.tabRegister': 'Sign up',
  'auth.displayName': 'What should we call you',
  'auth.email': 'E-mail',
  'auth.password': 'Password',
  'auth.passwordHint': 'At least 8 characters, an upper- and a lower-case letter and a digit',
  'auth.accountType': 'Account type',
  'auth.private': 'Private seller',
  'auth.dealer': 'Dealership',
  'auth.busy': 'One moment…',
  'auth.signIn': 'Sign in',
  'auth.register': 'Create account',
  'auth.forgot': 'Forgotten your password?',
  'auth.resetTitle': 'Password recovery',
  'auth.resetLead': 'We will e-mail you a link.',
  'auth.resetSending': 'Sending…',
  'auth.resetSend': 'Send the link',
  'auth.resetRemembered': 'I remember it now',
  'auth.checkMail': 'Check your e-mail',
  'auth.checkMailText':
    'If {email} is registered with AutoLot, we have sent a password link there.',
  'auth.backToSignIn': 'Back to sign in',

  'error.network': 'Could not reach the server.',
  'error.requestFailed': 'The request to {path} failed',
  'error.badResponse': 'Unexpected response from {path}',
}

export const dictionaries = { uk, en } as const
