﻿using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Singleton;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace KnightBus.Azure.Storage.Singleton
{
    internal class BlobLockHandle : ISingletonLockHandle
    {
        private readonly TimeSpan _leasePeriod;

        private DateTimeOffset _lastRenewal;
        private TimeSpan _lastRenewalLatency;

        public BlobLockHandle(string leaseId, string lockId, CloudBlockBlob blob, TimeSpan leasePeriod)
        {
            LeaseId = leaseId;
            LockId = lockId;
            _leasePeriod = leasePeriod;
            Blob = blob;
        }

        public string LeaseId { get; }
        public string LockId { get; }
        private CloudBlockBlob Blob { get; }

        public async Task<bool> RenewAsync(ILog log, CancellationToken cancellationToken)
        {
            try
            {
                AccessCondition condition = new AccessCondition
                {
                    LeaseId = LeaseId
                };
                DateTimeOffset requestStart = DateTimeOffset.UtcNow;
                await Blob.RenewLeaseAsync(condition, null, null, cancellationToken).ConfigureAwait(false);
                _lastRenewal = DateTime.UtcNow;
                _lastRenewalLatency = _lastRenewal - requestStart;

                // The next execution should occur after a normal delay.
                return true;
            }
            catch (StorageException exception)
            {
                if (exception.IsServerSideError())
                {
                    string msg = string.Format(CultureInfo.InvariantCulture, "Singleton lock renewal failed for blob '{0}'", LockId);
                    log.Warning(exception, msg);
                    return false; // The next execution should occur more quickly (try to renew the lease before it expires).
                }
                else
                {
                    // Log the details we've been accumulating to help with debugging this scenario
                    var leasePeriodMilliseconds = (int)_leasePeriod.TotalMilliseconds;
                    var lastRenewalFormatted = _lastRenewal.ToString("yyyy-MM-ddTHH:mm:ss.FFFZ", CultureInfo.InvariantCulture);
                    var millisecondsSinceLastSuccess = (int)(DateTime.UtcNow - _lastRenewal).TotalMilliseconds;
                    var lastRenewalMilliseconds = (int)_lastRenewalLatency.TotalMilliseconds;

                    var msg = string.Format(CultureInfo.InvariantCulture, "Singleton lock renewal failed for blob '{0}'. The last successful renewal completed at {1} ({2} milliseconds ago) with a duration of {3} milliseconds. The lease period was {4} milliseconds.",
                        LockId, lastRenewalFormatted, millisecondsSinceLastSuccess, lastRenewalMilliseconds, leasePeriodMilliseconds);
                    log.Error(exception, msg);

                    // If we've lost the lease or cannot re-establish it, we want to fail any
                    // in progress function execution
                    throw;
                }
            }
        }
        public async Task ReleaseAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Note that this call returns without throwing if the lease is expired. See the table at:
                // http://msdn.microsoft.com/en-us/library/azure/ee691972.aspx
                await Blob.ReleaseLeaseAsync(
                    new AccessCondition { LeaseId = LeaseId },
                    null,
                    null,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (StorageException exception)
            {
                if (exception.RequestInformation != null)
                {
                    if (exception.RequestInformation.HttpStatusCode == 404 ||
                        exception.RequestInformation.HttpStatusCode == 409)
                    {
                        // if the blob no longer exists, or there is another lease
                        // now active, there is nothing for us to release so we can
                        // ignore
                    }
                    else
                    {
                        throw;
                    }
                }
                else
                {
                    throw;
                }
            }
        }
    }
}