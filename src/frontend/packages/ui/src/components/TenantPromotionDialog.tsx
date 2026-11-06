import { useEffect, useRef, useState } from 'react';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import MenuItem from '@mui/material/MenuItem';
import Stack from '@mui/material/Stack';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';

/** The environment-bound values a promotion writes for one resource (decoupled from the API contract). */
export type TenantPromotionDialogEnvironmentSettings = {
  readonly resourceType: string;
  readonly resourceId: string;
  readonly settings: Readonly<Record<string, unknown>>;
};

/** The promotion the dialog asks for (decoupled from the API contract). */
export type TenantPromotionDialogRequest = {
  readonly sourceTenantId: string;
  readonly targetTenantId: string;
  readonly environmentSettings: readonly TenantPromotionDialogEnvironmentSettings[];
};

/** One resource a promotion creates, updates or leaves unchanged (decoupled from the API contract). */
export type TenantPromotionDialogChange = {
  readonly resourceType: string;
  readonly resourceId: string;
  readonly action: string;
};

/** Something to know about a promoted resource (decoupled from the API contract). */
export type TenantPromotionDialogNote = {
  readonly resourceType: string;
  readonly resourceId: string;
  readonly code: string;
  readonly detail: string;
  readonly settingName?: string | null | undefined;
};

/** A reason the promotion cannot be applied (decoupled from the API contract). */
export type TenantPromotionDialogError = {
  readonly resourceType: string;
  readonly detail: string;
  readonly resourceId?: string | null | undefined;
};

/** The report of a promotion preview or of an applied promotion (decoupled from the API contract). */
export type TenantPromotionDialogReport = {
  readonly sourceTenantId: string;
  readonly targetTenantId: string;
  readonly isApplied: boolean;
  readonly changes: readonly TenantPromotionDialogChange[];
  readonly notes: readonly TenantPromotionDialogNote[];
  readonly errors: readonly TenantPromotionDialogError[];
};

export type TenantPromotionDialogProps = {
  readonly open: boolean;
  /** The IDs of the team's tenants, offered as the source and the target. */
  readonly tenantIds: readonly string[];
  /** The last preview or applied report, or `undefined` while there is none. */
  readonly report?: TenantPromotionDialogReport | undefined;
  readonly isBusy?: boolean;
  /** A failure of the last request, such as a refusal naming a limit the caller may not change. */
  readonly error?: string | undefined;
  readonly onPreview: (request: TenantPromotionDialogRequest) => void;
  readonly onPromote: (request: TenantPromotionDialogRequest) => void;
  readonly onCancel: () => void;
};

const environmentSettingUnset = 'environment_setting_unset';
const hostManaged = 'host_managed';

type EnvironmentField = {
  readonly key: string;
  readonly resourceType: string;
  readonly resourceId: string;
  readonly settingName: string;
};

function fieldKey(resourceType: string, resourceId: string, settingName: string): string {
  return `${resourceType}-${resourceId}-${settingName}`;
}

/** The text of one note: a host-managed client reads as a client the promotion leaves. */
function noteText(note: TenantPromotionDialogNote): string {
  if (note.code === hostManaged) {
    return `The promotion leaves ${note.resourceType} ${note.resourceId}. ${note.detail}`;
  }
  return `${note.resourceType} ${note.resourceId}: ${note.detail}`;
}

type ParsedValue =
  { readonly isValid: true; readonly value: unknown } | { readonly isValid: false };

/** Parses a field's JSON text; blank text supplies no value. */
function parseValue(text: string): ParsedValue | undefined {
  const trimmed = text.trim();
  if (trimmed.length === 0) {
    return undefined;
  }
  try {
    return { isValid: true, value: JSON.parse(trimmed) as unknown };
  } catch {
    return { isValid: false };
  }
}

/**
 * Stateless promotion dialog for a team's tenants: pick a source and a target tenant, preview the promotion,
 * fill the environment-bound values the preview asks for, and promote. The selections and the typed values are
 * local because they are intrinsic to the controlled inputs (they reset each time the dialog opens); the report,
 * the busy flag and every failure are the container's to supply, as is the work of previewing and promoting.
 */
export function TenantPromotionDialog({
  open,
  tenantIds,
  report,
  isBusy = false,
  error,
  onPreview,
  onPromote,
  onCancel,
}: TenantPromotionDialogProps) {
  const [sourceTenantId, setSourceTenantId] = useState('');
  const [targetTenantId, setTargetTenantId] = useState('');
  const [texts, setTexts] = useState<Readonly<Record<string, string>>>({});
  const wasOpenRef = useRef(open);

  // Reset only on the rising edge of `open`, so a re-render while open never discards in-progress edits.
  useEffect(() => {
    if (open && !wasOpenRef.current) {
      setSourceTenantId('');
      setTargetTenantId('');
      setTexts({});
    }
    wasOpenRef.current = open;
  }, [open]);

  const isPair =
    sourceTenantId.length > 0 && targetTenantId.length > 0 && sourceTenantId !== targetTenantId;
  const isReportForPair =
    report !== undefined &&
    report.sourceTenantId === sourceTenantId &&
    report.targetTenantId === targetTenantId;
  const isApplied = isReportForPair && report.isApplied;

  const fields: EnvironmentField[] =
    isReportForPair && !report.isApplied
      ? report.notes.flatMap((note) =>
          note.code === environmentSettingUnset && note.settingName
            ? [
                {
                  key: fieldKey(note.resourceType, note.resourceId, note.settingName),
                  resourceType: note.resourceType,
                  resourceId: note.resourceId,
                  settingName: note.settingName,
                },
              ]
            : [],
        )
      : [];

  let hasInvalidValue = false;
  const grouped = new Map<
    string,
    { resourceType: string; resourceId: string; settings: Record<string, unknown> }
  >();
  for (const field of fields) {
    const parsed = parseValue(texts[field.key] ?? '');
    if (parsed === undefined) {
      continue;
    }
    if (!parsed.isValid) {
      hasInvalidValue = true;
      continue;
    }
    const groupKey = `${field.resourceType}\u0000${field.resourceId}`;
    const group = grouped.get(groupKey) ?? {
      resourceType: field.resourceType,
      resourceId: field.resourceId,
      settings: {},
    };
    group.settings[field.settingName] = parsed.value;
    grouped.set(groupKey, group);
  }

  const request: TenantPromotionDialogRequest = {
    sourceTenantId,
    targetTenantId,
    environmentSettings: [...grouped.values()],
  };
  const canPreview = isPair && !isBusy;
  const canPromote =
    isReportForPair &&
    !report.isApplied &&
    report.errors.length === 0 &&
    !hasInvalidValue &&
    !isBusy;

  return (
    <Dialog open={open} onClose={onCancel} fullWidth maxWidth="md">
      <DialogTitle>Promote tenant</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          {error && (
            <Alert severity="error" data-testid="promotion-error">
              {error}
            </Alert>
          )}
          <TextField
            select
            label="Source tenant"
            value={sourceTenantId}
            onChange={(event) => setSourceTenantId(event.target.value)}
            helperText="The tenant whose configuration is copied."
            fullWidth
            slotProps={{ htmlInput: { 'data-testid': 'promotion-source' } }}
          >
            {tenantIds.map((tenantId) => (
              <MenuItem key={tenantId} value={tenantId}>
                {tenantId}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            select
            label="Target tenant"
            value={targetTenantId}
            onChange={(event) => setTargetTenantId(event.target.value)}
            helperText="The tenant that receives it. Secrets, keys and environment-bound values stay with their tenant."
            fullWidth
            slotProps={{ htmlInput: { 'data-testid': 'promotion-target' } }}
          >
            {tenantIds.map((tenantId) => (
              <MenuItem key={tenantId} value={tenantId}>
                {tenantId}
              </MenuItem>
            ))}
          </TextField>
          {isReportForPair && report.errors.length > 0 && (
            <Stack spacing={1}>
              {report.errors.map((promotionError) => (
                <Alert
                  key={`${promotionError.resourceType}-${promotionError.resourceId ?? ''}-${promotionError.detail}`}
                  severity="error"
                  data-testid="promotion-report-error"
                >
                  {promotionError.resourceId
                    ? `${promotionError.resourceType} ${promotionError.resourceId}: ${promotionError.detail}`
                    : `${promotionError.resourceType}: ${promotionError.detail}`}
                </Alert>
              ))}
            </Stack>
          )}
          {isReportForPair && report.changes.length > 0 && (
            <Table size="small" aria-label="Changes" data-testid="promotion-changes">
              <TableHead>
                <TableRow>
                  <TableCell>Type</TableCell>
                  <TableCell>Resource</TableCell>
                  <TableCell>Action</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {report.changes.map((change) => (
                  <TableRow key={`${change.resourceType}-${change.resourceId}`}>
                    <TableCell>{change.resourceType}</TableCell>
                    <TableCell>{change.resourceId}</TableCell>
                    <TableCell>{change.action}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
          {isReportForPair && report.notes.length > 0 && (
            <Stack spacing={0.5} data-testid="promotion-notes">
              <Typography variant="subtitle2">Notes</Typography>
              {report.notes.map((note) => (
                <Typography
                  key={`${note.resourceType}-${note.resourceId}-${note.code}-${note.settingName ?? ''}-${note.detail}`}
                  variant="body2"
                >
                  {noteText(note)}
                </Typography>
              ))}
            </Stack>
          )}
          {fields.map((field) => {
            const parsed = parseValue(texts[field.key] ?? '');
            const isInvalid = parsed !== undefined && !parsed.isValid;
            return (
              <TextField
                key={field.key}
                label={`${field.resourceType} ${field.resourceId}: ${field.settingName}`}
                value={texts[field.key] ?? ''}
                onChange={(event) =>
                  setTexts((current) => ({ ...current, [field.key]: event.target.value }))
                }
                error={isInvalid}
                helperText={
                  isInvalid
                    ? 'Enter a valid JSON value.'
                    : 'A JSON value for the target. Leave empty to leave it unset.'
                }
                fullWidth
                slotProps={{
                  htmlInput: { 'data-testid': `environment-setting-${field.key}` },
                }}
              />
            );
          })}
          {isApplied && (
            <Alert severity="success" data-testid="promotion-applied">
              Promoted.
            </Alert>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        {isApplied ? (
          <Button variant="contained" onClick={onCancel} data-testid="promotion-close">
            Close
          </Button>
        ) : (
          <>
            <Button onClick={onCancel} disabled={isBusy} data-testid="promotion-cancel">
              Cancel
            </Button>
            <Button
              onClick={() => onPreview(request)}
              disabled={!canPreview}
              data-testid="promotion-preview"
            >
              Preview
            </Button>
            <Button
              variant="contained"
              onClick={() => onPromote(request)}
              disabled={!canPromote}
              data-testid="promotion-promote"
            >
              Promote
            </Button>
          </>
        )}
      </DialogActions>
    </Dialog>
  );
}
