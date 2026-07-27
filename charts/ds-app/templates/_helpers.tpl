{{- define "ds-app.image" -}}
{{ .Values.image.repository }}:{{ .Values.image.tag }}
{{- end -}}
