import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { expectNoA11yViolations } from '../../testing/a11y';
import { Alert } from './alert';
import { describedBy } from './described-by';
import { ErrorSummary } from './error-summary';
import { FormField } from './form-field';
import { Loading } from './loading';
import { StatusChip } from './status-chip';
import { Stepper } from './stepper';

@Component({
  imports: [Alert, Loading, FormField, StatusChip, Stepper, ErrorSummary],
  template: `
    <bb-alert id="err" variant="error" heading="Oops">Something failed</bb-alert>
    <bb-alert id="info" variant="info">Just so you know</bb-alert>
    <bb-loading label="Loading branches" />
    <bb-form-field label="Email address" controlId="email" hint="We never share it" error="Enter a valid email">
      <input id="email" class="field" type="email" aria-describedby="email-hint email-error" aria-invalid="true" />
    </bb-form-field>
    <bb-status-chip id="chip" tone="success">Confirmed</bb-status-chip>
    <bb-stepper [steps]="['Type', 'Branch', 'Time', 'Details']" [current]="1" />
    <bb-error-summary [items]="[{ id: 'email', message: 'Enter a valid email' }]" />
  `,
})
class Host {}

describe('shared UI', () => {
  let root: HTMLElement;

  beforeEach(() => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    root = fixture.nativeElement as HTMLElement;
  });

  it('shows an error alert with role alert, its heading and content', () => {
    const alert = root.querySelector('#err [role="alert"]');

    expect(alert?.textContent).toContain('Oops');
    expect(alert?.textContent).toContain('Something failed');
  });

  it('shows an information alert with role status', () => {
    expect(root.querySelector('#info [role="status"]')?.textContent).toContain('Just so you know');
  });

  it('announces loading with its label', () => {
    expect(root.querySelector('bb-loading [role="status"]')?.textContent).toContain('Loading branches');
  });

  it('links the form field label to its control and gives the hint and error ids', () => {
    expect(root.querySelector('label')?.getAttribute('for')).toBe('email');
    expect(root.querySelector('#email-hint')?.textContent).toContain('We never share it');
    expect(root.querySelector('#email-error')?.textContent).toContain('Enter a valid email');
  });

  it('marks the current step and says where the customer is', () => {
    expect(root.querySelector('bb-stepper p')?.textContent).toContain('Step 2 of 4: Branch');
    expect(root.querySelectorAll('bb-stepper [aria-current="step"]').length).toBe(1);
  });

  it('colours a chip by tone', () => {
    expect(root.querySelector('#chip span')?.className).toContain('chip-success');
  });

  it('lists the error summary and moves focus to the field when an entry is used', () => {
    const button = root.querySelector<HTMLButtonElement>('bb-error-summary button');
    document.body.appendChild(root);

    button?.click();

    expect(document.activeElement?.id).toBe('email');
    root.remove();
  });

  it('has no accessibility violations', async () => {
    await expectNoA11yViolations(root);
  });
});

describe('describedBy', () => {
  it('joins the hint and error ids that exist', () => {
    expect(describedBy('email', true, true)).toBe('email-hint email-error');
    expect(describedBy('email', true, false)).toBe('email-hint');
    expect(describedBy('email', false, true)).toBe('email-error');
  });

  it('returns null when there is nothing to describe the control', () => {
    expect(describedBy('email', false, false)).toBeNull();
  });
});
