import { describe, expect, it } from 'vitest'
import {
  distinctChoreAssignees,
  distinctChoreRooms,
  parseChoreImport,
} from './chore-import'
import { parseCsv } from './csv'

const header =
  'grocy_chore_id,title,description,room,estimated_minutes,is_active,frequency,interval,mode,assignment_policy,fixed_assignee,completion_history,grocy_created_at'

function parse(lines: string[]) {
  return parseChoreImport(parseCsv([header, ...lines].join('\n')))
}

describe('parseChoreImport', () => {
  it('parses a chore with execution history', () => {
    const result = parse([
      '27,Cambiare Lenzuola,,Camera,10,true,FixedIntervalDays,14,Dynamic,SpecificMember,Francesco,2026-08-14|admin;2026-09-05|admin;2026-09-20|admin,2026-09-04 18:49:28',
    ])

    expect(result.missing).toEqual([])
    expect(result.rows).toHaveLength(1)

    const row = result.rows[0]!
    expect(row.title).toBe('Cambiare Lenzuola')
    expect(row.room).toBe('Camera')
    expect(row.estimatedMinutes).toBe(10)
    expect(row.frequency).toBe('FixedIntervalDays')
    expect(row.interval).toBe(14)
    expect(row.assignmentPolicy).toBe('SpecificMember')
    expect(row.fixedAssignee).toBe('Francesco')
    expect(row.completions).toEqual(['2026-08-14', '2026-09-05', '2026-09-20'])
    expect(row.syntheticCompletionDate).toBeNull()
    expect(row.issues).toEqual([])
  })

  it('uses the creation date as a synthetic completion when history is empty', () => {
    const result = parse([
      '4,Pulire Vetri,,Generico,90,true,FixedIntervalDays,60,Dynamic,Both,,,2026-08-21 17:20:29',
    ])

    const row = result.rows[0]!
    expect(row.completions).toEqual([])
    expect(row.syntheticCompletionDate).toBe('2026-08-21')
    expect(row.issues).toEqual([])
  })

  it('flags a chore without history and without a creation date', () => {
    const result = parse(['4,Pulire Vetri,,Generico,90,true,FixedIntervalDays,60,Dynamic,Both,,,,'])

    expect(result.rows[0]!.issues).toEqual(['created'])
    expect(result.rows[0]!.syntheticCompletionDate).toBeNull()
  })

  it('flags missing title, invalid duration and invalid history', () => {
    const result = parse([
      '4,,,Generico,nope,true,FixedIntervalDays,0,Dynamic,SpecificMember,,2026-99-99|admin,2026-08-21 17:20:29',
    ])

    expect(result.rows[0]!.issues).toEqual(
      expect.arrayContaining(['title', 'minutes', 'interval', 'completion', 'assignee']),
    )
  })

  it('deduplicates completion dates and keeps the date part', () => {
    const result = parse([
      '4,Pulire Vetri,,Generico,90,true,Daily,1,Dynamic,Both,,2026-08-14|admin;2026-08-14|admin,2026-08-21 17:20:29',
    ])

    expect(result.rows[0]!.completions).toEqual(['2026-08-14'])
  })

  it('reports missing required headers', () => {
    const result = parseChoreImport(parseCsv('room,interval\nCucina,7'))

    expect(result.rows).toEqual([])
    expect(result.missing).toEqual(['title', 'estimatedMinutes'])
  })

  it('collects distinct room and assignee names', () => {
    const result = parse([
      '4,Pulire Vetri,,Cucina,90,true,Daily,1,Dynamic,SpecificMember,Francesco,2026-08-14|admin,2026-08-21 17:20:29',
      '5,Pulire Forno,,Cucina,30,true,Daily,1,Dynamic,SpecificMember,Cecilia,2026-08-14|admin,2026-08-21 17:20:29',
      '6,Pulire Bagno,,Bagno,20,true,Daily,1,Dynamic,Both,,,2026-08-21 17:20:29',
    ])

    expect(distinctChoreRooms(result.rows)).toEqual(['Cucina', 'Bagno'])
    expect(distinctChoreAssignees(result.rows)).toEqual(['Francesco', 'Cecilia'])
  })
})
