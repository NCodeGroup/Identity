import type { Meta, StoryObj } from '@storybook/react-vite';
import { SecretFormDialog, emptySecretFormValues } from './SecretFormDialog.js';

const meta: Meta<typeof SecretFormDialog> = {
  title: 'Management/SecretFormDialog',
  component: SecretFormDialog,
};
export default meta;

type Story = StoryObj<typeof SecretFormDialog>;

export const Create: Story = {
  args: {
    open: true,
    initialValues: { ...emptySecretFormValues, expiresWhen: '2027-01-01T00:00' },
    onSubmit: () => undefined,
    onCancel: () => undefined,
  },
};

export const WithError: Story = {
  args: {
    open: true,
    initialValues: { ...emptySecretFormValues, expiresWhen: '2027-01-01T00:00' },
    error: 'The secret could not be generated from the supplied parameters.',
    onSubmit: () => undefined,
    onCancel: () => undefined,
  },
};

export const WithDiscovery: Story = {
  args: {
    open: true,
    initialValues: { ...emptySecretFormValues, secretType: 'rsa', expiresWhen: '2027-01-01T00:00' },
    eccCurves: [
      { curveName: 'P-256', curveSizeBits: 256 },
      { curveName: 'P-384', curveSizeBits: 384 },
    ],
    algorithms: [
      {
        code: 'RS256',
        use: 'sig',
        secretTypes: ['rsa'],
        keyBitSizes: [{ minBits: 2048, maxBits: 16384, skipBits: 8 }],
      },
      {
        code: 'RSA-OAEP',
        use: 'enc',
        secretTypes: ['rsa'],
        keyBitSizes: [{ minBits: 2048, maxBits: 16384, skipBits: 8 }],
      },
      {
        code: 'ES256',
        use: 'sig',
        secretTypes: ['ecc'],
        keyBitSizes: [{ minBits: 256, maxBits: 256, skipBits: 0 }],
      },
    ],
    onSubmit: () => undefined,
    onCancel: () => undefined,
  },
};
