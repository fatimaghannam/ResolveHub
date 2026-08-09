import { useEffect, useState } from 'react'

function UserAvatar({ firstName, lastName, imagePath, className = '', ...props }) {
  const [imageFailed, setImageFailed] = useState(false)
  const initials = [firstName, lastName]
    .filter(Boolean)
    .map((name) => name[0])
    .join('')
    .toUpperCase()

  useEffect(() => setImageFailed(false), [imagePath])

  return (
    <span className={`profile-avatar ${className}`.trim()} {...props}>
      {imagePath && !imageFailed ? (
        <img src={imagePath} alt="" onError={() => setImageFailed(true)} />
      ) : initials}
    </span>
  )
}

export default UserAvatar
