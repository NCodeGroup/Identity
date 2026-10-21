import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { TenantFormDialog, emptyTenantFormValues } from './TenantFormDialog.js';

const acme = {
  tenantId: 'acme',
  displayName: 'Acme',
  issuer: 'https://acme.auth.example',
  mtlsEndpointAliasAuthority: 'https://acme.mtls.example',
  isDisabled: false,
};

describe('TenantFormDialog', () => {
  it('seeds the fields from initialValues when opened', () => {
    render(
      <TenantFormDialog
        open
        title="Edit tenant"
        submitLabel="Save"
        initialValues={{ ...acme, isDisabled: true }}
        isTenantIdLocked
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('tenant-id')).toHaveValue('acme');
    expect(screen.getByTestId('tenant-display-name')).toHaveValue('Acme');
    expect(screen.getByTestId('tenant-issuer')).toHaveValue('https://acme.auth.example');
    expect(screen.getByTestId('tenant-mtls-endpoint-alias')).toHaveValue(
      'https://acme.mtls.example',
    );
    expect(screen.getByRole('switch', { name: 'Disabled' })).toBeChecked();
  });

  it('locks the tenant ID of an existing tenant', () => {
    render(
      <TenantFormDialog
        open
        title="Edit tenant"
        submitLabel="Save"
        initialValues={acme}
        isTenantIdLocked
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('tenant-id')).toBeDisabled();
    expect(screen.getByText('A tenant ID cannot be changed.')).toBeInTheDocument();
    expect(screen.getByTestId('tenant-form-submit')).toBeEnabled();
  });

  it('disables submit until a valid tenant ID and a display name are entered', () => {
    render(
      <TenantFormDialog
        open
        title="Create tenant"
        submitLabel="Create"
        initialValues={emptyTenantFormValues}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('tenant-form-submit')).toBeDisabled();
    fireEvent.change(screen.getByTestId('tenant-display-name'), { target: { value: '   ' } });
    fireEvent.change(screen.getByTestId('tenant-id'), { target: { value: 'acme' } });
    expect(screen.getByTestId('tenant-form-submit')).toBeDisabled();
    fireEvent.change(screen.getByTestId('tenant-display-name'), { target: { value: 'New' } });
    expect(screen.getByTestId('tenant-form-submit')).toBeEnabled();
  });

  it.each(['Acme', '-acme', 'acme-', 'ac_me', 'a'.repeat(64)])(
    'flags the tenant ID %s as invalid and blocks submit',
    (tenantId) => {
      render(
        <TenantFormDialog
          open
          title="Create tenant"
          submitLabel="Create"
          initialValues={{ ...emptyTenantFormValues, displayName: 'Acme' }}
          onSubmit={vi.fn()}
          onCancel={vi.fn()}
        />,
      );
      fireEvent.change(screen.getByTestId('tenant-id'), { target: { value: tenantId } });
      expect(screen.getByTestId('tenant-id')).toHaveAttribute('aria-invalid', 'true');
      expect(screen.getByTestId('tenant-form-submit')).toBeDisabled();
    },
  );

  it('submits trimmed values', () => {
    const onSubmit = vi.fn();
    render(
      <TenantFormDialog
        open
        title="Create tenant"
        submitLabel="Create"
        initialValues={emptyTenantFormValues}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );
    fireEvent.change(screen.getByTestId('tenant-id'), { target: { value: ' acme-dev ' } });
    fireEvent.change(screen.getByTestId('tenant-display-name'), { target: { value: '  Acme  ' } });
    fireEvent.change(screen.getByTestId('tenant-issuer'), {
      target: { value: ' https://acme-dev.auth.example ' },
    });
    fireEvent.change(screen.getByTestId('tenant-mtls-endpoint-alias'), {
      target: { value: ' https://acme-dev.mtls.example ' },
    });
    fireEvent.click(screen.getByTestId('tenant-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith({
      tenantId: 'acme-dev',
      displayName: 'Acme',
      issuer: 'https://acme-dev.auth.example',
      mtlsEndpointAliasAuthority: 'https://acme-dev.mtls.example',
      isDisabled: false,
    });
  });

  it('surfaces a server error and blocks submit while submitting', () => {
    const onSubmit = vi.fn();
    render(
      <TenantFormDialog
        open
        title="Create tenant"
        submitLabel="Create"
        initialValues={{ ...acme, issuer: '' }}
        isSubmitting
        error="Boom"
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('tenant-form-error')).toHaveTextContent('Boom');
    expect(screen.getByTestId('tenant-form-submit')).toBeDisabled();
    expect(screen.getByTestId('tenant-form-submit')).toHaveTextContent('Saving…');
    expect(screen.getByTestId('tenant-form-cancel')).toBeDisabled();
  });

  it('submits the toggled disabled state', () => {
    const onSubmit = vi.fn();
    render(
      <TenantFormDialog
        open
        title="Edit tenant"
        submitLabel="Save"
        initialValues={{ ...acme, issuer: '', mtlsEndpointAliasAuthority: '' }}
        isTenantIdLocked
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );
    const toggle = screen.getByRole('switch', { name: 'Disabled' });
    fireEvent.click(toggle);
    expect(toggle).toBeChecked();
    fireEvent.click(screen.getByTestId('tenant-form-submit'));
    expect(onSubmit).toHaveBeenCalledExactlyOnceWith({
      tenantId: 'acme',
      displayName: 'Acme',
      issuer: '',
      mtlsEndpointAliasAuthority: '',
      isDisabled: true,
    });
  });

  it('keeps in-progress edits while open and reseeds from initialValues on reopen', () => {
    const props = {
      title: 'Edit tenant',
      submitLabel: 'Save',
      isTenantIdLocked: true,
      onSubmit: vi.fn(),
      onCancel: vi.fn(),
    };
    const globex = {
      tenantId: 'globex',
      displayName: 'Globex',
      issuer: 'https://globex.auth.example',
      mtlsEndpointAliasAuthority: '',
      isDisabled: true,
    };
    const { rerender } = render(<TenantFormDialog {...props} open initialValues={acme} />);
    fireEvent.change(screen.getByTestId('tenant-display-name'), {
      target: { value: 'Unsaved edit' },
    });

    rerender(<TenantFormDialog {...props} open initialValues={{ ...acme }} />);
    expect(screen.getByTestId('tenant-display-name')).toHaveValue('Unsaved edit');

    rerender(<TenantFormDialog {...props} open={false} initialValues={acme} />);
    rerender(<TenantFormDialog {...props} open initialValues={globex} />);
    expect(screen.getByTestId('tenant-id')).toHaveValue('globex');
    expect(screen.getByTestId('tenant-display-name')).toHaveValue('Globex');
    expect(screen.getByTestId('tenant-issuer')).toHaveValue('https://globex.auth.example');
    expect(screen.getByTestId('tenant-mtls-endpoint-alias')).toHaveValue('');
    expect(screen.getByRole('switch', { name: 'Disabled' })).toBeChecked();
  });

  it('invokes onCancel from the cancel button', () => {
    const onCancel = vi.fn();
    render(
      <TenantFormDialog
        open
        title="Create tenant"
        submitLabel="Create"
        initialValues={emptyTenantFormValues}
        onSubmit={vi.fn()}
        onCancel={onCancel}
      />,
    );
    fireEvent.click(screen.getByTestId('tenant-form-cancel'));
    expect(onCancel).toHaveBeenCalledOnce();
  });

  it('reports the trimmed address as it is entered', () => {
    const onAddressChange = vi.fn();
    render(
      <TenantFormDialog
        open
        title="Create tenant"
        submitLabel="Create"
        initialValues={emptyTenantFormValues}
        onAddressChange={onAddressChange}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    fireEvent.change(screen.getByTestId('tenant-id'), { target: { value: ' acme ' } });
    fireEvent.change(screen.getByTestId('tenant-issuer'), {
      target: { value: ' https://acme.example ' },
    });
    fireEvent.change(screen.getByTestId('tenant-mtls-endpoint-alias'), {
      target: { value: ' https://acme.mtls.example ' },
    });
    expect(onAddressChange).toHaveBeenLastCalledWith({
      tenantId: 'acme',
      issuer: 'https://acme.example',
      mtlsEndpointAliasAuthority: 'https://acme.mtls.example',
    });
  });

  it('shows the derived issuer and mTLS endpoint alias that empty fields resolve to', () => {
    render(
      <TenantFormDialog
        open
        title="Create tenant"
        submitLabel="Create"
        initialValues={{ ...emptyTenantFormValues, tenantId: 'acme', displayName: 'Acme' }}
        addressPreview={{
          issuer: 'https://acme.auth.example',
          isIssuerDerived: true,
          mtlsEndpointAliasAuthority: 'https://acme.mtls.example',
          isMtlsEndpointAliasAuthorityDerived: true,
          exists: false,
          failures: [],
        }}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByText('Leave empty to use https://acme.auth.example.')).toBeInTheDocument();
    expect(screen.getByTestId('tenant-issuer')).toHaveAttribute(
      'placeholder',
      'https://acme.auth.example',
    );
    expect(screen.getByText('Leave empty to use https://acme.mtls.example.')).toBeInTheDocument();
    expect(screen.getByTestId('tenant-mtls-endpoint-alias')).toHaveAttribute(
      'placeholder',
      'https://acme.mtls.example',
    );
    expect(screen.getByTestId('tenant-form-submit')).toBeEnabled();
  });

  it('flags a taken tenant ID and blocks submit', () => {
    render(
      <TenantFormDialog
        open
        title="Create tenant"
        submitLabel="Create"
        initialValues={{ ...emptyTenantFormValues, tenantId: 'acme', displayName: 'Acme' }}
        addressPreview={{
          issuer: null,
          isIssuerDerived: false,
          mtlsEndpointAliasAuthority: null,
          isMtlsEndpointAliasAuthorityDerived: false,
          exists: true,
          failures: [],
        }}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByText('A tenant with this ID already exists.')).toBeInTheDocument();
    expect(screen.getByTestId('tenant-id')).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByTestId('tenant-form-submit')).toBeDisabled();
  });

  it('ignores the existing tenant when its own ID is locked', () => {
    render(
      <TenantFormDialog
        open
        title="Edit tenant"
        submitLabel="Save"
        initialValues={acme}
        isTenantIdLocked
        addressPreview={{
          issuer: 'https://acme.auth.example',
          isIssuerDerived: false,
          mtlsEndpointAliasAuthority: 'https://acme.mtls.example',
          isMtlsEndpointAliasAuthorityDerived: false,
          exists: true,
          failures: [],
        }}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('tenant-form-submit')).toBeEnabled();
  });

  it('lists the reasons the address cannot be served and blocks submit', () => {
    render(
      <TenantFormDialog
        open
        title="Create tenant"
        submitLabel="Create"
        initialValues={{ ...acme, issuer: 'https://taken.example' }}
        addressPreview={{
          issuer: 'https://taken.example',
          isIssuerDerived: false,
          mtlsEndpointAliasAuthority: null,
          isMtlsEndpointAliasAuthorityDerived: false,
          exists: false,
          failures: [{ detail: 'Another tenant already has the issuer.', isConflict: true }],
        }}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('tenant-address-failures')).toHaveTextContent(
      'Another tenant already has the issuer.',
    );
    expect(screen.getByTestId('tenant-form-submit')).toBeDisabled();
  });

  it('describes the mTLS endpoint alias when the deployment derives none', () => {
    render(
      <TenantFormDialog
        open
        title="Create tenant"
        submitLabel="Create"
        initialValues={{ ...emptyTenantFormValues, tenantId: 'acme', displayName: 'Acme' }}
        addressPreview={{
          issuer: 'https://acme.auth.example',
          isIssuerDerived: true,
          mtlsEndpointAliasAuthority: null,
          isMtlsEndpointAliasAuthorityDerived: false,
          exists: false,
          failures: [],
        }}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(
      screen.getByText(/The https origin where clients authenticate with a certificate/),
    ).toBeInTheDocument();
    expect(screen.getByTestId('tenant-mtls-endpoint-alias')).toHaveAttribute(
      'placeholder',
      'e.g. https://acme.mtls.example.com',
    );
  });
});
