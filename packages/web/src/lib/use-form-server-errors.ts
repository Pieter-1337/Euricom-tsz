import { useState } from 'react';
import { parseServerError } from '#/lib/server-error';

interface FormWithSetFieldMeta {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  setFieldMeta: (field: never, updater: (prev: any) => any) => void;
}

export function useFormServerErrors(form: FormWithSetFieldMeta, fieldNames: readonly string[]) {
  const [serverError, setServerError] = useState<string | null>(null);

  function clearServerErrors() {
    setServerError(null);
    for (const field of fieldNames) {
      form.setFieldMeta(field as never, (prev) => ({
        ...prev,
        errorMap: { ...prev.errorMap, onServer: undefined },
      }));
    }
  }

  function handleApiError(e: unknown) {
    const apiErr = parseServerError(e);
    if (apiErr) {
      const fieldErrors = apiErr.fieldErrors;
      if (fieldErrors) {
        for (const [field, errs] of Object.entries(fieldErrors)) {
          const key = (field.charAt(0).toLowerCase() + field.slice(1)) as never;
          form.setFieldMeta(key, (prev) => ({
            ...prev,
            errorMap: { ...prev.errorMap, onServer: errs.map((fe) => fe.message) },
            isTouched: true,
          }));
        }
      }
      setServerError(apiErr.userMessage);
    } else {
      setServerError('Something went wrong.');
    }
  }

  return { serverError, clearServerErrors, handleApiError };
}
