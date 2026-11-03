import { useEffect, useRef, useState } from 'react';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogContentText from '@mui/material/DialogContentText';
import DialogTitle from '@mui/material/DialogTitle';
import MenuItem from '@mui/material/MenuItem';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';

/** The supported secret material types; the server generates the key material from this choice. */
export type SecretType = 'symmetric' | 'rsa' | 'ecc';

/** An elliptic curve offered for ECC secrets, discovered from the server capabilities. */
export type EccCurveOption = {
  readonly curveName: string;
  readonly curveSizeBits: number;
};

/** An inclusive range of legal key sizes (bits); `skipBits` 0 denotes a single fixed size. */
export type KeyBitSizesOption = {
  readonly minBits: number;
  readonly maxBits: number;
  readonly skipBits: number;
};

/** One secret type an algorithm accepts, with the key sizes legal for keys of that type. */
export type AlgorithmKeyTypeOption = {
  /** The secret-type discriminator (e.g. `rsa`, `ecc`) whose key material is compatible with the algorithm. */
  readonly secretType: string;
  readonly keyBitSizes: readonly KeyBitSizesOption[];
  /**
   * The curves keys of the secret type must be on for the algorithm, in its order of preference; absent or empty when
   * the algorithm does not restrict the curve. Two curves can share a key size, so the curve is not implied by it.
   */
  readonly curves?: readonly string[];
};

/** A signature or key-management algorithm offered for secrets, discovered from the server capabilities. */
export type AlgorithmOption = {
  readonly code: string;
  /** The key use the algorithm implies: `sig` for signature, `enc` for key-management. */
  readonly use: string;
  /**
   * The compatible secret types, each with its legal key sizes: one algorithm, such as `ECDH-ES`, may accept several
   * secret types with different sizes (ADR-1389).
   */
  readonly keyTypes: readonly AlgorithmKeyTypeOption[];
};

/** The first legal key-size range of the algorithm for the secret type, if it accepts that type. */
const keySizeRangeFor = (
  algorithm: AlgorithmOption | undefined,
  secretType: string,
): KeyBitSizesOption | undefined =>
  algorithm?.keyTypes.find((keyType) => keyType.secretType === secretType)?.keyBitSizes[0];

/** The curves the algorithm accepts keys of the secret type on; empty when it does not restrict the curve. */
const curvesFor = (algorithm: AlgorithmOption | undefined, secretType: string): readonly string[] =>
  algorithm?.keyTypes.find((keyType) => keyType.secretType === secretType)?.curves ?? [];

/** The secret create form's own field shape. `expiresWhen` is a `datetime-local` string (local time). */
export type SecretFormValues = {
  readonly secretType: SecretType;
  readonly keySizeBits: number;
  /** The ECC curve name (empty selects a curve by key size); only meaningful for the `ecc` type. */
  readonly curve: string;
  readonly use: string;
  readonly algorithm: string;
  readonly expiresWhen: string;
};

export type SecretFormDialogProps = {
  readonly open: boolean;
  readonly initialValues: SecretFormValues;
  readonly isSubmitting?: boolean;
  readonly error?: string | undefined;
  /** The ECC curves to offer when the ECC type is selected, discovered from server capabilities. */
  readonly eccCurves?: readonly EccCurveOption[];
  /** The signature/key-management algorithms to offer, discovered from server capabilities. */
  readonly algorithms?: readonly AlgorithmOption[];
  readonly onSubmit: (values: SecretFormValues) => void;
  readonly onCancel: () => void;
};

/** A zeroed starting point for the create form; containers seed a sensible default expiry. */
export const emptySecretFormValues: SecretFormValues = {
  secretType: 'symmetric',
  keySizeBits: 256,
  curve: '',
  use: '',
  algorithm: '',
  expiresWhen: '',
};

const secretTypes: readonly { readonly value: SecretType; readonly label: string }[] = [
  { value: 'symmetric', label: 'Symmetric' },
  { value: 'rsa', label: 'RSA' },
  { value: 'ecc', label: 'ECC' },
];

/**
 * Stateless secret create form rendered in a dialog. Secret key material is generated server-side and
 * revealed exactly once (ADR-1050), so there is no edit path here — only create. The field state is
 * local because it is intrinsic to the controlled inputs (it reseeds on the rising edge of `open`), so
 * the component still renders in isolation under Storybook/RTL. Persistence, the one-time reveal, and
 * server-error handling live in the container that wires the mutation.
 */
export function SecretFormDialog({
  open,
  initialValues,
  isSubmitting = false,
  error,
  eccCurves,
  algorithms,
  onSubmit,
  onCancel,
}: SecretFormDialogProps) {
  const [values, setValues] = useState<SecretFormValues>(initialValues);
  const wasOpenRef = useRef(open);

  useEffect(() => {
    if (open && !wasOpenRef.current) {
      setValues(initialValues);
    }
    wasOpenRef.current = open;
  }, [open, initialValues]);

  // When the server advertises algorithms, the algorithm, use, and key-size inputs are driven by discovery
  // (ADR-1067) instead of free-text defaults; only algorithms compatible with the chosen type are offered.
  const algorithmsProvided = (algorithms?.length ?? 0) > 0;
  const applicableAlgorithms = (algorithms ?? []).filter((algorithm) =>
    algorithm.keyTypes.some((keyType) => keyType.secretType === values.secretType),
  );
  const selectedAlgorithm = applicableAlgorithms.find(
    (algorithm) => algorithm.code === values.algorithm,
  );
  const keySizeRange = keySizeRangeFor(selectedAlgorithm, values.secretType);
  // An algorithm bound to curves, such as ES256K, offers only those, since a curve of the same size is not one it
  // accepts (ADR-1389).
  const algorithmCurves = curvesFor(selectedAlgorithm, values.secretType);
  const offeredCurves =
    algorithmCurves.length > 0
      ? (eccCurves ?? []).filter((curve) => algorithmCurves.includes(curve.curveName))
      : eccCurves;

  const canSubmit = values.keySizeBits > 0 && values.expiresWhen.trim().length > 0 && !isSubmitting;

  const handleSubmit = () => {
    if (!canSubmit) {
      return;
    }
    onSubmit({
      secretType: values.secretType,
      keySizeBits: values.keySizeBits,
      curve: values.secretType === 'ecc' ? values.curve : '',
      use: values.use.trim(),
      algorithm: values.algorithm.trim(),
      expiresWhen: values.expiresWhen,
    });
  };

  return (
    <Dialog open={open} onClose={onCancel} fullWidth maxWidth="sm">
      <DialogTitle>Create secret</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          {error && (
            <Alert severity="error" data-testid="secret-form-error">
              {error}
            </Alert>
          )}
          <DialogContentText component="div">
            The key material is generated by the server and shown only once after creation. Store it
            securely — it can never be retrieved again.
          </DialogContentText>
          <TextField
            select
            label="Type"
            value={values.secretType}
            onChange={(event) =>
              setValues((v) => ({
                ...v,
                secretType: event.target.value as SecretType,
                // Reset the discovered algorithm/use when the type changes; they are re-derived on selection.
                ...(algorithmsProvided ? { algorithm: '', use: '' } : {}),
              }))
            }
            fullWidth
            slotProps={{ htmlInput: { 'data-testid': 'secret-type' } }}
          >
            {secretTypes.map((type) => (
              <MenuItem key={type.value} value={type.value}>
                {type.label}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            label="Key size (bits)"
            type="number"
            value={values.keySizeBits}
            onChange={(event) =>
              setValues((v) => ({ ...v, keySizeBits: Number(event.target.value) }))
            }
            required
            fullWidth
            helperText={
              keySizeRange
                ? keySizeRange.minBits === keySizeRange.maxBits
                  ? `Fixed at ${keySizeRange.minBits} bits for ${values.algorithm}.`
                  : `Allowed: ${keySizeRange.minBits}–${keySizeRange.maxBits} bits for ${values.algorithm}.`
                : undefined
            }
            slotProps={{
              htmlInput: {
                'data-testid': 'secret-key-size',
                min: keySizeRange?.minBits ?? 1,
                ...(keySizeRange ? { max: keySizeRange.maxBits } : {}),
                ...(keySizeRange && keySizeRange.skipBits > 0
                  ? { step: keySizeRange.skipBits }
                  : {}),
              },
            }}
          />
          {values.secretType === 'ecc' && offeredCurves && offeredCurves.length > 0 && (
            <TextField
              select
              label="Curve"
              value={values.curve}
              onChange={(event) => {
                const curve = event.target.value;
                const match = offeredCurves.find((c) => c.curveName === curve);
                setValues((v) => ({
                  ...v,
                  curve,
                  keySizeBits: match ? match.curveSizeBits : v.keySizeBits,
                }));
              }}
              fullWidth
              slotProps={{ htmlInput: { 'data-testid': 'secret-curve' } }}
            >
              {algorithmCurves.length === 0 && <MenuItem value="">By key size</MenuItem>}
              {offeredCurves.map((curve) => (
                <MenuItem key={curve.curveName} value={curve.curveName}>
                  {curve.curveName}
                </MenuItem>
              ))}
            </TextField>
          )}
          <TextField
            label="Use (optional)"
            value={values.use}
            onChange={(event) => setValues((v) => ({ ...v, use: event.target.value }))}
            placeholder="e.g. sig"
            fullWidth
            // When algorithms are discovered the use is derived from the chosen algorithm, not entered by hand.
            slotProps={{ htmlInput: { 'data-testid': 'secret-use', readOnly: algorithmsProvided } }}
          />
          {algorithmsProvided ? (
            <TextField
              select
              label="Algorithm (optional)"
              value={selectedAlgorithm ? values.algorithm : ''}
              onChange={(event) => {
                const code = event.target.value;
                const match = applicableAlgorithms.find((algorithm) => algorithm.code === code);
                setValues((v) => {
                  const range = keySizeRangeFor(match, v.secretType);
                  const inRange =
                    range !== undefined &&
                    v.keySizeBits >= range.minBits &&
                    v.keySizeBits <= range.maxBits;
                  // A curve the algorithm does not accept gives way to the first one it does, with that curve's size.
                  const curves = v.secretType === 'ecc' ? curvesFor(match, v.secretType) : [];
                  const curveOption =
                    curves.length > 0 && !curves.includes(v.curve)
                      ? curves
                          .map((name) => eccCurves?.find((c) => c.curveName === name))
                          .find((c) => c !== undefined)
                      : undefined;
                  return {
                    ...v,
                    algorithm: code,
                    use: match ? match.use : '',
                    keySizeBits: curveOption
                      ? curveOption.curveSizeBits
                      : range && !inRange
                        ? range.minBits
                        : v.keySizeBits,
                    ...(curveOption ? { curve: curveOption.curveName } : {}),
                  };
                });
              }}
              fullWidth
              slotProps={{ htmlInput: { 'data-testid': 'secret-algorithm' } }}
            >
              <MenuItem value="">Any</MenuItem>
              {applicableAlgorithms.map((algorithm) => (
                <MenuItem key={algorithm.code} value={algorithm.code}>
                  {algorithm.code}
                </MenuItem>
              ))}
            </TextField>
          ) : (
            <TextField
              label="Algorithm (optional)"
              value={values.algorithm}
              onChange={(event) => setValues((v) => ({ ...v, algorithm: event.target.value }))}
              placeholder="e.g. RS256"
              fullWidth
              slotProps={{ htmlInput: { 'data-testid': 'secret-algorithm' } }}
            />
          )}
          <TextField
            label="Expires"
            type="datetime-local"
            value={values.expiresWhen}
            onChange={(event) => setValues((v) => ({ ...v, expiresWhen: event.target.value }))}
            required
            fullWidth
            slotProps={{
              inputLabel: { shrink: true },
              htmlInput: { 'data-testid': 'secret-expires' },
            }}
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onCancel} disabled={isSubmitting} data-testid="secret-form-cancel">
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handleSubmit}
          disabled={!canSubmit}
          data-testid="secret-form-submit"
        >
          {isSubmitting ? 'Creating…' : 'Create'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
