import { describe, expect, it } from 'vitest'
import { addDays, daysBetween, freeRatio, plannerWindow, shiftWindow } from './planner'

describe('addDays', () => {
  it('crosses month and year boundaries', () => {
    expect(addDays('2026-06-28', 5)).toBe('2026-07-03')
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01')
    expect(addDays('2026-03-01', -1)).toBe('2026-02-28')
  })
})

describe('plannerWindow', () => {
  it('spans the requested number of calendar days with an exclusive end', () => {
    expect(plannerWindow('2026-06-10', 30)).toEqual({ from: '2026-06-10', to: '2026-07-10' })
  })

  it('never collapses to an empty window', () => {
    expect(plannerWindow('2026-06-10', 0)).toEqual({ from: '2026-06-10', to: '2026-06-11' })
  })
})

describe('shiftWindow', () => {
  it('keeps the window length while moving it', () => {
    const shifted = shiftWindow('2026-06-10', '2026-07-10', 7)

    expect(shifted).toEqual({ from: '2026-06-17', to: '2026-07-17' })
    expect(daysBetween(shifted.from, shifted.to)).toBe(30)
  })
})

describe('freeRatio', () => {
  it('clamps to a 0–1 range', () => {
    expect(freeRatio(60, 120)).toBe(0.5)
    expect(freeRatio(0, 120)).toBe(0)
    expect(freeRatio(500, 120)).toBe(1)
    expect(freeRatio(-10, 120)).toBe(0)
  })

  it('treats a zero window as fully busy instead of dividing by zero', () => {
    expect(freeRatio(30, 0)).toBe(0)
  })
})
