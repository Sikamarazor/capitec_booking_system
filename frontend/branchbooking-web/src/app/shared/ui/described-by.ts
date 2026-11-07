/** The `aria-describedby` value for a control whose hint and error elements use the `bb-form-field` id scheme. */
export function describedBy(controlId: string, hasHint: boolean, hasError: boolean): string | null {
  const ids = [hasHint ? `${controlId}-hint` : null, hasError ? `${controlId}-error` : null].filter(
    (id): id is string => id !== null,
  );
  return ids.length > 0 ? ids.join(' ') : null;
}
