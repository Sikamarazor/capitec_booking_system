import { ValidationErrors, ValidatorFn } from '@angular/forms';

export const FULL_NAME_MAX_LENGTH = 100;
export const EMAIL_MAX_LENGTH = 254;

export type CustomerField = 'fullName' | 'email' | 'phone';

function text(value: unknown): string {
  return String(value ?? '').trim();
}

/** Same rule as the server: 2 to 100 characters after trimming. */
export const fullNameValidator: ValidatorFn = (control): ValidationErrors | null => {
  const value = text(control.value);
  if (value.length === 0) {
    return { required: true };
  }
  if (value.length < 2) {
    return { minlength: true };
  }
  return value.length > FULL_NAME_MAX_LENGTH ? { maxlength: true } : null;
};

/** One `@`, text on both sides, a dot inside the domain that is not at either end or doubled, no spaces, at most 254 characters. */
export function isValidEmail(email: string): boolean {
  if (email.length === 0 || email.length > EMAIL_MAX_LENGTH || /\s/.test(email)) {
    return false;
  }

  const at = email.indexOf('@');
  if (at <= 0 || at !== email.lastIndexOf('@') || at === email.length - 1) {
    return false;
  }

  const domain = email.slice(at + 1);
  return domain.includes('.') && !domain.startsWith('.') && !domain.endsWith('.') && !domain.includes('..');
}

export const emailValidator: ValidatorFn = (control): ValidationErrors | null => {
  const value = text(control.value);
  if (value.length === 0) {
    return { required: true };
  }
  return isValidEmail(value) ? null : { email: true };
};

/** Spaces, hyphens and brackets are ignored; an optional leading `+`; then 9 to 15 digits. */
export function isValidPhone(phone: string): boolean {
  const cleaned = phone.trim().replace(/[ ()-]/g, '');
  const digits = cleaned.startsWith('+') ? cleaned.slice(1) : cleaned;
  return /^\d{9,15}$/.test(digits);
}

export const phoneValidator: ValidatorFn = (control): ValidationErrors | null => {
  const value = text(control.value);
  if (value.length === 0) {
    return { required: true };
  }
  return isValidPhone(value) ? null : { phone: true };
};

const NOUNS: Record<CustomerField, string> = {
  fullName: 'full name',
  email: 'email address',
  phone: 'phone number',
};

/** The sentence shown under a field, or `null` when the field is fine. */
export function customerFieldMessage(field: CustomerField, errors: ValidationErrors | null): string | null {
  if (!errors) {
    return null;
  }
  if (errors['required']) {
    return `Enter your ${NOUNS[field]}.`;
  }
  if (errors['minlength']) {
    return 'Your full name must be at least 2 characters.';
  }
  if (errors['maxlength']) {
    return `Your full name must be ${FULL_NAME_MAX_LENGTH} characters or fewer.`;
  }
  if (errors['email']) {
    return 'Enter an email address like name@example.com.';
  }
  if (errors['phone']) {
    return 'Enter a phone number with 9 to 15 digits, for example 082 123 4567.';
  }
  if (errors['server']) {
    return `Check your ${NOUNS[field]}.`;
  }
  return null;
}
