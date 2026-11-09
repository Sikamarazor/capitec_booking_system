import { TestBed } from '@angular/core/testing';
import { aBranch, aType, anAvailability } from '../../testing/api-fixtures';
import { BookingDraftService } from './booking-draft.service';

describe('BookingDraftService', () => {
  let draft: BookingDraftService;

  beforeEach(() => {
    draft = TestBed.inject(BookingDraftService);
  });

  it('starts at the first step with nothing chosen', () => {
    expect(draft.step()).toBe(0);
    expect(draft.appointmentType()).toBeNull();
    expect(draft.branch()).toBeNull();
    expect(draft.slot()).toBeNull();
    expect(draft.details()).toEqual({ fullName: '', email: '', phone: '' });
  });

  it('keeps the time when the same branch is chosen again', () => {
    draft.chooseBranch(aBranch);
    draft.date.set('2026-11-03');
    draft.slot.set(anAvailability.slots[0]);

    draft.chooseBranch({ ...aBranch });

    expect(draft.slot()).not.toBeNull();
    expect(draft.date()).toBe('2026-11-03');
  });

  it('clears the time when a different branch is chosen', () => {
    draft.chooseBranch(aBranch);
    draft.date.set('2026-11-03');
    draft.slot.set(anAvailability.slots[0]);

    draft.chooseBranch({ ...aBranch, id: 'branch-2', name: 'Other Branch' });

    expect(draft.branch()?.id).toBe('branch-2');
    expect(draft.slot()).toBeNull();
    expect(draft.date()).toBeNull();
  });

  it('reset forgets everything, including the typed details', () => {
    draft.step.set(3);
    draft.appointmentType.set(aType);
    draft.chooseBranch(aBranch);
    draft.details.set({ fullName: 'Thandi Nkosi', email: 'thandi@example.com', phone: '0821234567' });

    draft.reset();

    expect(draft.step()).toBe(0);
    expect(draft.appointmentType()).toBeNull();
    expect(draft.branch()).toBeNull();
    expect(draft.details()).toEqual({ fullName: '', email: '', phone: '' });
  });
});
