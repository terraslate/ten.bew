using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ten.bew.Caching
{
    [Serializable]
    class CacheEntry
    {
        private DateTime _created;
        private string _lockedBy;
        private Guid _lockKey;
        private DateTime _lockedByExpiry;
        private DateTime _lastAccessed;
        private CacheEntryTypeEnum _cacheEntryType;
        private TimeSpan _timeToLive;

        public CacheEntry(TimeSpan timeToLive, CacheEntryTypeEnum cacheEntryType)
        {
            _timeToLive = timeToLive;
            _created = DateTime.UtcNow;
            _lastAccessed = _created;
            _cacheEntryType = cacheEntryType;
        }

        public byte[] Data
        {
            get;
            set;
        }

        public DateTime Created
        {
            get
            {
                return _created;
            }
        }

        public DateTime Expires
        {
            get
            {
                DateTime rv = (_cacheEntryType == CacheEntryTypeEnum.Expiry ? _created : _lastAccessed);
                rv = rv.Add(_timeToLive);
                return rv;
            }
        }

        public string LockedBy
        {
            get
            {
                return _lockedBy;
            }
        }

        public DateTime LockedByExpiry
        {
            get
            {
                return _lockedByExpiry;
            }
        }

        public DateTime LastAccessed
        {
            get
            {
                return _created;
            }
        }

        public Guid Lock(string userName, TimeSpan timeToLive)
        {
            Guid rv = Guid.Empty;

            if(_lockedBy == null)
            {
                _lockedBy = userName;
                _lockedByExpiry = DateTime.UtcNow.Add(timeToLive);
                _lockKey = rv = Guid.NewGuid();
            }

            return rv;
        }

        public bool Unlock(Guid lockKey)
        {
            bool rv = false;

            if(_lockKey.Equals(Guid.Empty) == false)
            {
                if(lockKey.Equals(_lockKey))
                {
                    _lockKey = Guid.Empty;
                    _lockedBy = null;
                    _lockedByExpiry = DateTime.MinValue;
                    rv = true;
                }
            }

            return rv;
        }
    }
}
