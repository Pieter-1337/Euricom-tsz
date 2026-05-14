type FieldMetaLike = {
  errorMap?: Record<string, unknown>;
};

export function hasClientSideError(fieldMeta: Record<string, FieldMetaLike>): boolean {
  return Object.values(fieldMeta).some((meta) =>
    Object.entries(meta.errorMap ?? {}).some(([source, value]) => source !== 'onServer' && value != null),
  );
}
