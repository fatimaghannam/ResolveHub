import { FileText } from 'lucide-react'

function formatFileSize(bytes) {
  if (bytes < 1024 * 1024) return `${Math.max(1, Math.ceil(bytes / 1024))} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

function formatFileType(contentType) {
  if (!contentType) return 'File'
  const subtype = contentType.split('/')[1]?.split(/[;+]/)[0]
  return subtype ? subtype.toUpperCase() : 'File'
}

function TicketAttachments({ attachments = [], onDownload, showEmpty = true }) {
  if (attachments.length === 0 && !showEmpty) return null

  return (
    <section className="ticket-attachments" aria-labelledby="ticket-attachments-title">
      <h2 id="ticket-attachments-title">Attachments</h2>
      {attachments.length === 0
        ? showEmpty && <p className="ticket-attachments__empty">No attachments.</p>
        : <div className="ticket-attachments__list">
          {attachments.map((file) => (
            <div className="attachment-row" key={file.id}>
              <FileText size={17} aria-hidden="true" />
              <span>{file.fileName}</span>
              <small>{formatFileSize(file.fileSizeBytes)} · {formatFileType(file.contentType)}</small>
              <button type="button" onClick={() => onDownload(file)}>Download</button>
            </div>
          ))}
        </div>}
    </section>
  )
}

export default TicketAttachments
