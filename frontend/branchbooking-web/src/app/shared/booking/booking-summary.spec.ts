import { TestBed } from '@angular/core/testing';
import { expectNoA11yViolations } from '../../testing/a11y';
import { aBooking } from '../../testing/api-fixtures';
import { Booking } from '../../core/api/api.models';
import { textOf } from '../../testing/dom';
import { BookingSummary } from './booking-summary';

describe('BookingSummary', () => {
  function render(booking: Booking, showContact = false) {
    const fixture = TestBed.createComponent(BookingSummary);
    fixture.componentRef.setInput('booking', booking);
    fixture.componentRef.setInput('showContact', showContact);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the appointment, branch, time on the branch clock, name and what to bring', () => {
    const text = textOf(render(aBooking()));

    expect(text).toContain('Open an account');
    expect(text).toContain('Test Branch');
    expect(text).toContain('1 Test Street, Johannesburg, Gauteng');
    expect(text).toContain('08:00 to 08:30');
    expect(text).toContain('Thandi Nkosi');
    expect(text).toContain('Bring your ID document.');
    expect(text).toContain('Confirmed');
  });

  it('shows the time in the branch zone, not the browser zone', () => {
    const text = textOf(
      render(
        aBooking({
          branch: { ...aBooking().branch, timeZoneId: 'Pacific/Auckland' },
        }),
      ),
    );

    expect(text).toContain('19:00 to 19:30');
  });

  it('shows the masked contact details only when asked', () => {
    expect(textOf(render(aBooking()))).not.toContain('t*****@example.com');

    const text = textOf(render(aBooking(), true));
    expect(text).toContain('t*****@example.com');
    expect(text).toContain('*******567');
  });

  it('marks a cancelled booking and says when it was cancelled', () => {
    const text = textOf(render(aBooking({ status: 'Cancelled', cancelledAtUtc: '2026-11-02T09:00:00+00:00' })));

    expect(text).toContain('Cancelled');
    expect(text).toContain('at 11:00');
  });

  it('has no accessibility violations', async () => {
    await expectNoA11yViolations(render(aBooking(), true));
  });
});
