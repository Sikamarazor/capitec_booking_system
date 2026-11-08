import { FormControl } from '@angular/forms';
import {
  customerFieldMessage,
  emailValidator,
  fullNameValidator,
  isValidEmail,
  isValidPhone,
  phoneValidator,
} from './customer-validation';

describe('customer validation', () => {
  it('accepts names of 2 to 100 characters after trimming', () => {
    expect(fullNameValidator(new FormControl('  Li  '))).toBeNull();
    expect(fullNameValidator(new FormControl('a'.repeat(100)))).toBeNull();
  });

  it('rejects empty, one-character and over-long names', () => {
    expect(fullNameValidator(new FormControl('   '))).toEqual({ required: true });
    expect(fullNameValidator(new FormControl('L'))).toEqual({ minlength: true });
    expect(fullNameValidator(new FormControl('a'.repeat(101)))).toEqual({ maxlength: true });
  });

  it('accepts ordinary email addresses', () => {
    expect(isValidEmail('thandi@example.com')).toBe(true);
    expect(isValidEmail('first.last+tag@sub.example.co.za')).toBe(true);
  });

  it('rejects malformed email addresses', () => {
    for (const bad of ['', 'plain', '@example.com', 'a@', 'a@b', 'a@@b.com', 'a b@example.com', 'a@.com', 'a@example.', 'a@exa..mple.com']) {
      expect(isValidEmail(bad)).toBe(false);
    }
    expect(isValidEmail(`${'a'.repeat(250)}@b.co`)).toBe(false);
  });

  it('email validator reports required and email errors', () => {
    expect(emailValidator(new FormControl(''))).toEqual({ required: true });
    expect(emailValidator(new FormControl('nope'))).toEqual({ email: true });
    expect(emailValidator(new FormControl(' thandi@example.com '))).toBeNull();
  });

  it('accepts phone numbers with spaces, hyphens, brackets and a leading plus', () => {
    expect(isValidPhone('082 123 4567')).toBe(true);
    expect(isValidPhone('+27 (82) 123-4567')).toBe(true);
    expect(isValidPhone('123456789')).toBe(true);
  });

  it('rejects phone numbers that are too short, too long or not digits', () => {
    for (const bad of ['', '12345678', '1234567890123456', '082-abc-4567', '++27821234567', '27+821234567']) {
      expect(isValidPhone(bad)).toBe(false);
    }
  });

  it('phone validator reports required and phone errors', () => {
    expect(phoneValidator(new FormControl(''))).toEqual({ required: true });
    expect(phoneValidator(new FormControl('abc'))).toEqual({ phone: true });
    expect(phoneValidator(new FormControl('0821234567'))).toBeNull();
  });

  it('writes a sentence for each kind of error and none for a good field', () => {
    expect(customerFieldMessage('fullName', { required: true })).toBe('Enter your full name.');
    expect(customerFieldMessage('email', { required: true })).toBe('Enter your email address.');
    expect(customerFieldMessage('phone', { required: true })).toBe('Enter your phone number.');
    expect(customerFieldMessage('fullName', { minlength: true })).toContain('at least 2');
    expect(customerFieldMessage('fullName', { maxlength: true })).toContain('100');
    expect(customerFieldMessage('email', { email: true })).toContain('name@example.com');
    expect(customerFieldMessage('phone', { phone: true })).toContain('9 to 15 digits');
    expect(customerFieldMessage('email', { server: true })).toBe('Check your email address.');
    expect(customerFieldMessage('email', null)).toBeNull();
  });
});
