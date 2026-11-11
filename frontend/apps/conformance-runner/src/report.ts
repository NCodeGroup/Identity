/**
 * Report formatting (UI-free, unit-tested) — renders a {@link ProbeReport} as either a human-readable table for a
 * terminal or a stable JSON document for CI consumption.
 */

import type { ProbeOutcome, ProbeReport } from '@ncode/conformance-core';

const OUTCOME_LABEL: Record<ProbeOutcome, string> = {
  verified: 'VERIFIED',
  failed: 'FAILED',
  skipped: 'SKIPPED',
};

/** Renders the report as aligned, human-readable lines (outcome · feature — detail), plus a totals summary. */
export function formatReport(report: ProbeReport, issuer: string): string {
  const lines: string[] = [`Conformance probe runner — ${issuer}`, ''];
  const width = Math.max(...Object.values(OUTCOME_LABEL).map((label) => label.length));
  for (const result of report.results) {
    lines.push(`  ${OUTCOME_LABEL[result.outcome].padEnd(width)}  ${result.feature}`);
    lines.push(`  ${' '.repeat(width)}  ${result.detail} [${result.reference}]`);
  }
  lines.push('');
  lines.push(
    `  ${report.totals.verified} verified · ${report.totals.failed} failed · ${report.totals.skipped} skipped`,
  );
  return lines.join('\n');
}

/** Renders the report as a stable, pretty-printed JSON document. */
export function toJson(report: ProbeReport, issuer: string): string {
  return JSON.stringify({ issuer, ...report }, null, 2);
}

/** The process exit code: non-zero when any probe failed, so the runner is a usable CI gate. */
export function exitCode(report: ProbeReport): number {
  return report.totals.failed > 0 ? 1 : 0;
}
