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
    secretTypes: ['symmetric', 'rsa', 'ecc'],
    onSubmit: () => undefined,
    onCancel: () => undefined,
  },
};

export const RemoteCustody: Story = {
  args: {
    open: true,
    initialValues: { ...emptySecretFormValues, expiresWhen: '2027-01-01T00:00' },
    secretTypes: ['symmetric', 'rsa', 'ecc'],
    remoteKeyProviders: [
      {
        name: 'aws-kms',
        algorithms: [
          { algorithm: 'ES256', use: 'sig', secretType: 'ecc', keySizeBits: 256, curve: 'P-256' },
          { algorithm: 'RS256', use: 'sig', secretType: 'rsa', keySizeBits: 2048, curve: null },
          { algorithm: 'RSA-OAEP-256', use: 'enc', secretType: 'rsa', keySizeBits: 3072 },
        ],
      },
      {
        name: 'gcp-kms',
        algorithms: [{ algorithm: 'PS256', use: 'sig', secretType: 'rsa', keySizeBits: 3072 }],
      },
    ],
    onSubmit: () => undefined,
    onCancel: () => undefined,
  },
};

export const WithError: Story = {
  args: {
    open: true,
    initialValues: { ...emptySecretFormValues, expiresWhen: '2027-01-01T00:00' },
    error: 'The secret could not be generated from the supplied parameters.',
    secretTypes: ['symmetric', 'rsa', 'ecc'],
    onSubmit: () => undefined,
    onCancel: () => undefined,
  },
};

export const WithDiscovery: Story = {
  args: {
    open: true,
    initialValues: { ...emptySecretFormValues, secretType: 'rsa', expiresWhen: '2027-01-01T00:00' },
    secretTypes: ['symmetric', 'rsa', 'ecc', 'okp'],
    eccCurves: [
      { curveName: 'P-256', curveSizeBits: 256 },
      { curveName: 'P-384', curveSizeBits: 384 },
    ],
    algorithms: [
      {
        code: 'RS256',
        use: 'sig',
        keyTypes: [
          {
            secretType: 'rsa',
            keyBitSizes: [{ minBits: 2048, maxBits: 16384, skipBits: 8 }],
            curves: [],
          },
        ],
      },
      {
        code: 'RSA-OAEP',
        use: 'enc',
        keyTypes: [
          {
            secretType: 'rsa',
            keyBitSizes: [{ minBits: 2048, maxBits: 16384, skipBits: 8 }],
            curves: [],
          },
        ],
      },
      {
        code: 'EdDSA',
        use: 'sig',
        keyTypes: [
          {
            secretType: 'okp',
            keyBitSizes: [
              { minBits: 256, maxBits: 256, skipBits: 0 },
              { minBits: 456, maxBits: 456, skipBits: 0 },
            ],
            curves: [
              { curveName: 'Ed25519', curveSizeBits: 256 },
              { curveName: 'Ed448', curveSizeBits: 456 },
            ],
          },
        ],
      },
      {
        code: 'ES256',
        use: 'sig',
        keyTypes: [
          {
            secretType: 'ecc',
            keyBitSizes: [{ minBits: 256, maxBits: 256, skipBits: 0 }],
            curves: [{ curveName: 'P-256', curveSizeBits: 256 }],
          },
        ],
      },
    ],
    onSubmit: () => undefined,
    onCancel: () => undefined,
  },
};
