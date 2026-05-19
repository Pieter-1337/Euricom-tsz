type FieldMetaLike = {
  errorMap?: Record<string, unknown>;
};

export function hasFormError(fieldMeta: Record<string, FieldMetaLike>): boolean {
  return Object.values(fieldMeta).some((meta) => Object.values(meta.errorMap ?? {}).some((value) => value != null));
}
