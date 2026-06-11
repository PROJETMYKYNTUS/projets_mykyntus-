import { TestBed } from '@angular/core/testing';

import { SubServiceService } from './sub-service.service';

describe('SubService', () => {
  let service: SubServiceService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(SubServiceService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
