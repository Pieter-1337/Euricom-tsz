import { useEffect, useRef, useState } from 'react';
import { parseServerError } from '#/lib/server-error';

interface FormWithSetFieldMeta {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  setFieldMeta: (field: never, updater: (prev: any) => any) => void;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  store: { subscribe: (cb: () => void) => { unsubscribe: () => void }; state: { values: any } };
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  state: { values: any };
}

export function useFormServerErrors(form: FormWithSetFieldMeta, fieldNames: readonly string[]) {
  const [serverError, setServerError] = useState<string | null>(null);
  const errorAtValuesRef = useRef<unknown>(null);

  useEffect(() => {
    const sub = form.store.subscribe(() => {
      if (errorAtValuesRef.current != null && form.state.values !== errorAtValuesRef.current) {
        errorAtValuesRef.current = null;
        setServerError(null);
      }
    });
    return () => sub.unsubscribe();
  }, [form]);

  function clearServerErrors() {
    errorAtValuesRef.current = null;
    setServerError(null);
    for (const field of fieldNames) {
      form.setFieldMeta(field as never, (prev) => ({
        ...prev,
        errorMap: { ...(prev?.errorMap ?? {}), onServer: undefined },
      }));
    }
  }

  function handleApiError(e: unknown) {
    const apiErr = parseServerError(e);
    if (apiErr) {
      const fieldErrors = apiErr.fieldErrors;
      if (fieldErrors) {
        const known = new Set(fieldNames);
        for (const [field, errs] of Object.entries(fieldErrors)) {
          const key = field.charAt(0).toLowerCase() + field.slice(1);
          if (!known.has(key)) continue;
          form.setFieldMeta(key as never, (prev) => ({
            ...prev,
            errorMap: { ...prev?.errorMap, onServer: errs.map((fe) => fe.message) },
            isTouched: true,
          }));
        }
      }
      setServerError(apiErr.userMessage);
    } else {
      setServerError('Something went wrong.');
    }
    errorAtValuesRef.current = form.state.values;
  }

  return { serverError, clearServerErrors, handleApiError };
}
