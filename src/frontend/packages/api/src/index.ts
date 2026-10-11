export type {
  ClientResource,
  CollectionResource,
  CreateClientRequest,
  CreateTenantRequest,
  ServerResource,
  TenantResource,
  UpdateClientFields,
  UpdateTenantFields,
} from './contracts.js';
export { replacePatch, type JsonPatchDocument, type JsonPatchOperation } from './jsonPatch.js';
export {
  GLOBAL_SCOPE,
  isGlobalScope,
  scopeApiPrefix,
  scopeKey,
  tenantScope,
  type ManagementScope,
} from './scope.js';
