import { Camera, Trash2 } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import UserAvatar from '../../components/common/UserAvatar.jsx'
import { removeProfilePhoto, uploadProfilePhoto } from '../../services/profileService.js'
import { accountStatusClassName, formatAccountStatus } from '../../utils/accountStatus.js'
import { formatLocalDateTime } from '../../utils/dateTime.js'

function ProfilePage() {
  const { user, role, updateUser } = useOutletContext()
  const fileInputRef = useRef(null)
  const [isSavingPhoto, setIsSavingPhoto] = useState(false)
  const [photoError, setPhotoError] = useState('')
  const [showRemoveConfirmation, setShowRemoveConfirmation] = useState(false)
  const fullName = [user?.firstName, user?.lastName].filter(Boolean).join(' ')
  const status = user?.status ?? (user?.isActive === false ? 'Inactive' : 'Active')

  useEffect(() => {
    if (!showRemoveConfirmation) return undefined
    const closeOnEscape = (event) => {
      if (event.key === 'Escape' && !isSavingPhoto) setShowRemoveConfirmation(false)
    }
    document.addEventListener('keydown', closeOnEscape)
    return () => document.removeEventListener('keydown', closeOnEscape)
  }, [showRemoveConfirmation, isSavingPhoto])

  async function handlePhotoSelected(event) {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (!file) return
    setPhotoError('')
    setIsSavingPhoto(true)
    try {
      const response = await uploadProfilePhoto(file)
      updateUser({ profileImagePath: response.profileImagePath })
    } catch (error) {
      setPhotoError(error.message)
    } finally {
      setIsSavingPhoto(false)
    }
  }

  async function handleRemovePhoto() {
    setPhotoError('')
    setIsSavingPhoto(true)
    try {
      await removeProfilePhoto()
      updateUser({ profileImagePath: null })
      setShowRemoveConfirmation(false)
    } catch (error) {
      setPhotoError(error.message)
    } finally {
      setIsSavingPhoto(false)
    }
  }

  return (
    <div className="admin-user-details-page profile-page">
      <section className="page-heading admin-user-details-heading">
        <h2>Profile</h2>
        <p>View your account information, access level, and current status.</p>
      </section>
      <section className="panel admin-user-details-card">
        <header className="admin-user-identity">
          <div className="profile-photo-control">
            <button
              type="button"
              className="profile-photo-control__button"
              onClick={() => fileInputRef.current?.click()}
              disabled={isSavingPhoto}
              aria-label={user?.profileImagePath ? 'Change profile photo' : 'Upload profile photo'}
            >
              <UserAvatar
                className="admin-user-identity__avatar"
                firstName={user?.firstName}
                lastName={user?.lastName}
                imagePath={user?.profileImagePath}
                aria-hidden="true"
              />
              <span className="profile-photo-control__camera" aria-hidden="true"><Camera size={14} /></span>
            </button>
            <input
              ref={fileInputRef}
              className="visually-hidden"
              type="file"
              accept="image/jpeg,image/png,image/webp,.jpg,.jpeg,.png,.webp"
              onChange={handlePhotoSelected}
            />
          </div>
          <div className="admin-user-identity__content">
            <h3>{fullName}</h3>
            <a href={`mailto:${user?.email}`}>{user?.email}</a>
            <div className="profile-photo-actions">
              <button type="button" onClick={() => fileInputRef.current?.click()} disabled={isSavingPhoto}>
                {isSavingPhoto ? 'Saving…' : user?.profileImagePath ? 'Change photo' : 'Upload photo'}
              </button>
              {user?.profileImagePath && (
                <button type="button" className="profile-photo-actions__remove" onClick={() => { setPhotoError(''); setShowRemoveConfirmation(true) }} disabled={isSavingPhoto}>
                  <Trash2 size={13} /> Remove photo
                </button>
              )}
            </div>
            {photoError && <p className="profile-photo-error" role="alert">{photoError}</p>}
          </div>
        </header>

        <div className="admin-user-details-divider" />
        <div className="admin-user-information-grid">
          <section aria-labelledby="profile-information-title">
            <h4 id="profile-information-title">Profile Information</h4>
            <dl className="admin-user-information-list">
              <div><dt>Full name</dt><dd>{fullName}</dd></div>
              <div><dt>Email</dt><dd className="admin-user-information-email">{user?.email}</dd></div>
              <div><dt>Role</dt><dd>{role}</dd></div>
            </dl>
          </section>
          <section aria-labelledby="account-information-title">
            <h4 id="account-information-title">Account Information</h4>
            <dl className="admin-user-information-list">
              <div><dt>Account status</dt><dd><span className={`user-status user-status--${accountStatusClassName(status)}`}>{formatAccountStatus(status)}</span></dd></div>
              <div><dt>Created</dt><dd><time dateTime={user?.createdDate}>{formatLocalDateTime(user?.createdDate)}</time></dd></div>
            </dl>
          </section>
        </div>
      </section>
      {showRemoveConfirmation && (
        <>
          <div
            className="dialog-backdrop"
            aria-hidden="true"
            onClick={() => !isSavingPhoto && setShowRemoveConfirmation(false)}
          />
          <section
            className="dialog"
            role="dialog"
            aria-modal="true"
            aria-labelledby="remove-profile-photo-title"
            aria-describedby="remove-profile-photo-description"
          >
            <h2 id="remove-profile-photo-title">Remove profile photo?</h2>
            <p id="remove-profile-photo-description">
              Your profile photo will be removed and your initials will be shown instead.
            </p>
            {photoError && <p className="form-error" role="alert">{photoError}</p>}
            <div className="dialog__actions">
              <button
                autoFocus
                type="button"
                className="button button--secondary"
                onClick={() => setShowRemoveConfirmation(false)}
                disabled={isSavingPhoto}
              >
                Cancel
              </button>
              <button
                type="button"
                className="button button--danger"
                onClick={handleRemovePhoto}
                disabled={isSavingPhoto}
              >
                {isSavingPhoto ? 'Removing…' : 'Remove'}
              </button>
            </div>
          </section>
        </>
      )}
    </div>
  )
}

export default ProfilePage
