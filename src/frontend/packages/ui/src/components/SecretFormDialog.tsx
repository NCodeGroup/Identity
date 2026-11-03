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

/** An elliptic curve, by its JWK `crv` name and the size of its keys, discovered from the server capabilities. */
export type CurveOption = {
  readonly curveName: string;
  readonly curveSizeBits: number;
};

/** An inclusive range of legal key sizes (bits); `skipBits` 0 denotes a single fixed size. */
export type KeyBitSizesOption = {
  readonly minBits: number;
  readonly maxBits: number;
  readonly skipBits: number;
};

/** One secret type an algorithm accepts, with the key sizes and curves legal for keys of that type. */
export type AlgorithmKeyTypeOption = {
  /** The secret-type discriminator (e.g. `rsa`, `ecc`, `okp`) whose key material is compatible with the algorithm. */
  readonly secretType: string;
  readonly keyBitSizes: readonly KeyBitSizesOption[];
  /**
   * The curves keys of the secret type must be on for the algorithm, in its order of preference, each with its key
   * size; empty when the type has no curve. Two curves can share a key size, so the curve is not implied by it.
   */
  readonly curves: readonly CurveOption[];
};

/** A signature or key-management algorithm offered for secrets, discovered from the server capabilities. */
export type AlgorithmOption = {
  readonly code: string;
  /** The key use the algorithm implies: `sig` for signature, `enc` for key-management. */
  readonly use: string;
  /**
   * The compatible secret types, each with its legal key sizes and curves: one algorithm, such as `ECDH-ES`, may
   * accept several secret types with different sizes and curves (ADR-1389).
   */
  readonly keyTypes: readonly AlgorithmKeyTypeOption[];
};

/** The secret type whose curve catalog the `eccCurves` capability lists. */
const eccSecretType = 'ecc';

const secretTypeLabels: Readonly<Record<string, string>> = { symmetric: 'Symmetric' };

/** A display label for a secret-type discriminator, such as `RSA` for `rsa` or `OKP` for `okp`. */
const secretTypeLabel = (secretType: string): string =>
  secretTypeLabels[secretType] ?? secretType.toUpperCase();

const keyTypeFor = (
  algorithm: AlgorithmOption | undefined,
  secretType: string,
): AlgorithmKeyTypeOption | undefined =>
  algorithm?.keyTypes.find((keyType) => keyType.secretType === secretType);

/** Whether the key size is one of the ranges; any size is legal when there are none. */
const isLegalSize = (ranges: readonly KeyBitSizesOption[], keySizeBits: number): boolean =>
  ranges.length === 0 ||
  ranges.some(
    (range) =>
      keySizeBits >= range.minBits &&
      keySizeBits <= range.maxBits &&
      (range.skipBits === 0
        ? keySizeBits === range.minBits
        : (keySizeBits - range.minBits) % range.skipBits === 0),
  );

/** Joins the values as `a`, `a or b`, or `a, b or c`. */
const joinOr = (values: readonly string[]): string =>
  values.length <= 1 ? values.join('') : `${values.slice(0, -1).join(', ')} or ${values.at(-1)}`;

/** Describes the legal key sizes of the algorithm, listing every size or range it accepts. */
const describeKeySizes = (ranges: readonly KeyBitSizesOption[], code: string): string => {
  const sizes = ranges.map((range) =>
    range.minBits === range.maxBits ? `${range.minBits}` : `${range.minBits}–${range.maxBits}`,
  );
  const fixed = ranges.length === 1 && ranges.every((range) => range.minBits === range.maxBits);
  return fixed
    ? `Fixed at ${joinOr(sizes)} bits for ${code}.`
    : `Allowed: ${joinOr(sizes)} bits for ${code}.`;
};

/**
 * The curves offered for the secret type: the selected algorithm's when it names any, otherwise the curves every
 * discovered algorithm names for the type, together with the server's elliptic-curve catalog for the `ecc` type.
 */
const curvesFor = (
  secretType: string,
  selectedAlgorithm: AlgorithmOption | undefined,
  algorithms: readonly AlgorithmOption[],
  eccCurves: readonly CurveOption[],
): readonly CurveOption[] => {
  const algorithmCurves = keyTypeFor(selectedAlgorithm, secretType)?.curves ?? [];
  if (algorithmCurves.length > 0) {
    return algorithmCurves;
  }
  const discovered = [
    ...(secretType === eccSecretType ? eccCurves : []),
    ...algorithms.flatMap((algorithm) => keyTypeFor(algorithm, secretType)?.curves ?? []),
  ];
  return discovered.filter(
    (curve, index) => discovered.findIndex((c) => c.curveName === curve.curveName) === index,
  );
};

/** The secret create form's own field shape. `expiresWhen` is a `datetime-local` string (local time). */
export type SecretFormValues = {
  /**
   * The preferred secret-type discriminator (e.g. `symmetric`, `rsa`, `ecc`, `okp`). The form offers only the types
   * the server advertises, so a type it does not offer gives way to the first one it does.
   */
  readonly secretType: string;
  /** The key size, used when no curve is selected; a selected curve fixes the key size instead. */
  readonly keySizeBits: number;
  /** The curve name, required for a secret type with curves and empty for one without. */
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
  /**
   * The secret types to offer, discovered from server capabilities. None are offered while they load, and the form
   * cannot be submitted until one is.
   */
  readonly secretTypes: readonly string[];
  /** The server's elliptic-curve catalog, offered for the `ecc` type, discovered from server capabilities. */
  readonly eccCurves?: readonly CurveOption[];
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
  secretTypes,
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

  // The secret types are discovered from the server (ADR-1150), so a type a package contributes, such as `okp`, is
  // offered without the form knowing it. The selected type is always one the form offers: the seeded type when it is
  // offered, otherwise the first offered type, and none while the types load, so the select never holds a value it
  // has no option for.
  const secretType = secretTypes.includes(values.secretType)
    ? values.secretType
    : (secretTypes[0] ?? '');

  // When the server advertises algorithms, the algorithm, use, key-size and curve inputs are driven by discovery
  // (ADR-1067) instead of free-text defaults; only algorithms compatible with the chosen type are offered.
  const discovered = algorithms ?? [];
  const algorithmsProvided = discovered.length > 0;
  const applicableAlgorithms = discovered.filter((algorithm) =>
    algorithm.keyTypes.some((keyType) => keyType.secretType === secretType),
  );
  const selectedAlgorithm = applicableAlgorithms.find(
    (algorithm) => algorithm.code === values.algorithm,
  );
  const keySizeRanges = keyTypeFor(selectedAlgorithm, secretType)?.keyBitSizes ?? [];
  const [singleRange] = keySizeRanges;
  // An algorithm bound to curves, such as ES256K or EdDSA, offers only those, since a curve of the same size is not
  // one it accepts (ADR-1389); a curve fixes the key size. Two curves can share a size, so a secret type with curves
  // takes a curve and never leaves it to the size.
  const offeredCurves = curvesFor(secretType, selectedAlgorithm, discovered, eccCurves ?? []);
  const selectedCurve = offeredCurves.find((curve) => curve.curveName === values.curve);
  const curveRequired = offeredCurves.length > 0;
  const keySizeBits = selectedCurve?.curveSizeBits ?? values.keySizeBits;

  const canSubmit =
    secretType.length > 0 &&
    keySizeBits > 0 &&
    (!curveRequired || selectedCurve !== undefined) &&
    isLegalSize(keySizeRanges, keySizeBits) &&
    values.expiresWhen.trim().length > 0 &&
    !isSubmitting;

  const handleSubmit = () => {
    if (!canSubmit) {
      return;
    }
    onSubmit({
      secretType,
      keySizeBits,
      curve: selectedCurve ? selectedCurve.curveName : '',
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
            value={secretType}
            onChange={(event) => {
              const chosenType = event.target.value;
              // A curve belongs to its type, so the type's first curve is selected; the discovered algorithm and use
              // are re-derived on selection, so no algorithm narrows the curves yet. The shown key size carries over
              // to a type without curves.
              const [firstCurve] = curvesFor(chosenType, undefined, discovered, eccCurves ?? []);
              setValues((v) => ({
                ...v,
                secretType: chosenType,
                curve: firstCurve ? firstCurve.curveName : '',
                keySizeBits,
                ...(algorithmsProvided ? { algorithm: '', use: '' } : {}),
              }));
            }}
            fullWidth
            error={secretTypes.length === 0}
            helperText={secretTypes.length === 0 ? 'No secret types are available.' : undefined}
            slotProps={{ htmlInput: { 'data-testid': 'secret-type' } }}
          >
            {secretTypes.map((secretType) => (
              <MenuItem key={secretType} value={secretType}>
                {secretTypeLabel(secretType)}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            label="Key size (bits)"
            type="number"
            value={keySizeBits}
            onChange={(event) =>
              setValues((v) => ({ ...v, keySizeBits: Number(event.target.value) }))
            }
            required
            fullWidth
            error={!isLegalSize(keySizeRanges, keySizeBits)}
            helperText={
              keySizeRanges.length > 0
                ? describeKeySizes(keySizeRanges, values.algorithm)
                : undefined
            }
            slotProps={{
              htmlInput: {
                'data-testid': 'secret-key-size',
                // The selected curve fixes the key size.
                readOnly: selectedCurve !== undefined,
                min:
                  keySizeRanges.length > 0
                    ? Math.min(...keySizeRanges.map((range) => range.minBits))
                    : 1,
                ...(keySizeRanges.length > 0
                  ? { max: Math.max(...keySizeRanges.map((range) => range.maxBits)) }
                  : {}),
                ...(keySizeRanges.length === 1 && singleRange && singleRange.skipBits > 0
                  ? { step: singleRange.skipBits }
                  : {}),
              },
            }}
          />
          {offeredCurves.length > 0 && (
            <TextField
              select
              label="Curve"
              value={selectedCurve ? selectedCurve.curveName : ''}
              // The selected curve fixes the key size, which is derived from it.
              onChange={(event) => setValues((v) => ({ ...v, curve: event.target.value }))}
              required
              fullWidth
              error={!selectedCurve}
              slotProps={{ htmlInput: { 'data-testid': 'secret-curve' } }}
            >
              {!selectedCurve && (
                <MenuItem value="" disabled>
                  Choose a curve
                </MenuItem>
              )}
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
                  const keyType = keyTypeFor(match, secretType);
                  const curves = keyType?.curves ?? [];
                  // A curve the algorithm does not accept gives way to the first one it does, which fixes the size.
                  const curveOption =
                    curves.find((curve) => curve.curveName === v.curve) ?? curves[0];
                  const ranges = keyType?.keyBitSizes ?? [];
                  const [firstRange] = ranges;
                  return {
                    ...v,
                    secretType,
                    algorithm: code,
                    use: match ? match.use : '',
                    // A size the algorithm does not accept gives way to its smallest legal one.
                    keySizeBits:
                      !curveOption && firstRange && !isLegalSize(ranges, keySizeBits)
                        ? firstRange.minBits
                        : keySizeBits,
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
