import { HealthPanel } from './components/HealthPanel'

export default function App() {
  return (
    <main className="mx-auto flex min-h-dvh max-w-2xl flex-col justify-center gap-6 p-6">
      <div>
        <h1 className="text-3xl font-semibold tracking-tight">AutoLot</h1>
        <p className="mt-1 text-sm text-neutral-500 dark:text-neutral-400">
          Майданчик продажу авто з аукціоном. Каркас проєкту.
        </p>
      </div>

      <HealthPanel />
    </main>
  )
}
